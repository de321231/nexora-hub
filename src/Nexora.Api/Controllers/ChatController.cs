using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.Services;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Controllers;

[ApiController]
[Route("api/v1/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private const int MaxHistoryMessages = 20;

    private readonly AppDbContext _db;
    private readonly IQuotaService _quota;
    private readonly ILiteLlmClient _llm;
    private readonly IAuditService _audit;
    private readonly ILogger<ChatController> _logger;

    public ChatController(AppDbContext db, IQuotaService quota, ILiteLlmClient llm, IAuditService audit, ILogger<ChatController> logger)
    {
        _db = db;
        _quota = quota;
        _llm = llm;
        _audit = audit;
        _logger = logger;
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> Conversations()
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        var list = await _db.ChatConversations.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new ConversationDto
            {
                Id = c.Id,
                Title = c.Title,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                MessageCount = _db.ChatMessages.Count(m => m.ConversationId == c.Id)
            }).ToListAsync();
        return Ok(list);
    }

    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<IActionResult> Messages(Guid id)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        var conv = await _db.ChatConversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (conv == null) return NotFound(new { error = "Unterhaltung nicht gefunden." });
        var messages = await _db.ChatMessages.AsNoTracking()
            .Where(m => m.ConversationId == id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
        return Ok(messages.Select(DtoMapper.ToDto).ToList());
    }

    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> DeleteConversation(Guid id)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        var conv = await _db.ChatConversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (conv == null) return NotFound(new { error = "Unterhaltung nicht gefunden." });
        _db.ChatConversations.Remove(conv);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Unterhaltung gelöscht." });
    }

    /// <summary>
    /// Sendet eine Nachricht. Lizenzprüfung, Modellberechtigung und
    /// Kontingentkontrolle erfolgen ausschließlich serverseitig.
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest req)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        if (!_llm.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Der KI-Dienst (LiteLLM) ist nicht konfiguriert." });

        var license = await _quota.ResolveActiveLicenseAsync(userId, null, default);
        if (license == null)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Keine aktive Lizenz für Nexora AI vorhanden." });

        // Modellberechtigung prüfen.
        var allowedModel = await (from lm in _db.LicenseModels
                                  join m in _db.AiModels on lm.AiModelId equals m.Id
                                  where lm.LicenseId == license.Id && m.IsActive && m.ModelId == req.ModelId
                                  select m).FirstOrDefaultAsync();
        if (allowedModel == null)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = $"Das Modell '{req.ModelId}' ist für diese Lizenz nicht freigeschaltet." });

        // Unterhaltung ermitteln/anlegen.
        ChatConversation? conversation = null;
        if (req.ConversationId.HasValue)
        {
            conversation = await _db.ChatConversations.FirstOrDefaultAsync(c => c.Id == req.ConversationId && c.UserId == userId);
            if (conversation == null) return NotFound(new { error = "Unterhaltung nicht gefunden." });
        }

        var history = conversation == null
            ? new List<ChatMessage>()
            : await _db.ChatMessages.Where(m => m.ConversationId == conversation.Id)
                .OrderByDescending(m => m.CreatedAt).Take(MaxHistoryMessages)
                .OrderBy(m => m.CreatedAt).ToListAsync();

        var messages = new List<LiteLlmMessage>();
        if (!string.IsNullOrWhiteSpace(req.SystemPrompt))
            messages.Add(new("system", req.SystemPrompt!));
        messages.AddRange(history.Select(h => new LiteLlmMessage(h.Role == ChatRole.Assistant ? "assistant" : "user", h.Content)));
        messages.Add(new("user", req.Message));

        var estimatedInput = QuotaService.EstimateTokens(string.Concat(messages.Select(m => m.Content)));
        var requestedOutput = req.MaxOutputTokens ?? (int?)license.MaxTokensPerRequest ?? 0;

        // Kontingent reservieren (parallel sicher).
        var decision = await _quota.ReserveAsync(userId, license, estimatedInput, requestedOutput, default);
        if (!decision.Allowed || decision.Reservation == null)
        {
            _audit.Add(userId, AuditAction.QuotaExceeded, "License", license.Id.ToString(), decision.Reason ?? "Kontingent überschritten", HttpContext.Connection.RemoteIpAddress?.ToString());
            await _db.SaveChangesAsync();
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = decision.Reason });
        }

        var reservation = decision.Reservation;
        var sw = Stopwatch.StartNew();
        LiteLlmResult result;
        try
        {
            result = await _llm.ChatAsync(req.ModelId, messages, req.MaxOutputTokens, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var code = ex is LiteLlmNotConfiguredException ? "not_configured" : "llm_error";
            await _quota.ReconcileAsync(reservation, 0, 0, success: false, code, (int)sw.ElapsedMilliseconds, null,
                req.ModelId, allowedModel.Id, license.ProductId, default);
            _logger.LogWarning(ex, "Chat-Anfrage fehlgeschlagen für Benutzer {User}", userId);
            var status = ex is LiteLlmNotConfiguredException ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status502BadGateway;
            return StatusCode(status, new { error = ex.Message });
        }
        sw.Stop();

        // Unterhaltung / Nachrichten speichern.
        var now = DateTime.UtcNow;
        if (conversation == null)
        {
            var title = req.Message.Length > 60 ? req.Message[..60] : req.Message;
            conversation = new ChatConversation { UserId = userId, LicenseId = license.Id, Title = title, CreatedAt = now, UpdatedAt = now };
            _db.ChatConversations.Add(conversation);
        }
        else
        {
            conversation.UpdatedAt = now;
            conversation.LicenseId = license.Id;
        }

        var userMsg = new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = ChatRole.User,
            Content = req.Message,
            ModelId = req.ModelId,
            InputTokens = result.InputTokens,
            CreatedAt = now
        };
        var assistantMsg = new ChatMessage
        {
            ConversationId = conversation.Id,
            Role = ChatRole.Assistant,
            Content = result.Content,
            ModelId = result.Model,
            OutputTokens = result.OutputTokens,
            CreatedAt = now.AddMilliseconds(1)
        };
        _db.ChatMessages.Add(userMsg);
        _db.ChatMessages.Add(assistantMsg);

        await _quota.ReconcileAsync(reservation, result.InputTokens, result.OutputTokens, success: true, null,
            (int)sw.ElapsedMilliseconds, result.CostUsd, result.Model, allowedModel.Id, license.ProductId, default);

        await _db.SaveChangesAsync();

        await _db.Entry(license).Reference(l => l.Product).LoadAsync();
        var quota = await _quota.BuildStatusAsync(userId, license, default);

        return Ok(new ChatCompletionResponse
        {
            ConversationId = conversation.Id,
            MessageId = assistantMsg.Id,
            Content = result.Content,
            ModelId = result.Model,
            InputTokens = result.InputTokens,
            OutputTokens = result.OutputTokens,
            TokensEstimated = result.TokensEstimated,
            CostUsd = result.CostUsd,
            Quota = quota
        });
    }
}
