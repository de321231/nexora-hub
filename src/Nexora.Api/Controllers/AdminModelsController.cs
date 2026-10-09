using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.Services;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Controllers;

[ApiController]
[Route("api/v1/admin/models")]
[Authorize(Policy = "Admin")]
public class AdminModelsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILiteLlmClient _llm;
    private readonly IAuditService _audit;

    public AdminModelsController(AppDbContext db, ILiteLlmClient llm, IAuditService audit)
    {
        _db = db;
        _llm = llm;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var models = await _db.AiModels.AsNoTracking().OrderBy(m => m.ModelId).ToListAsync();
        return Ok(models.Select(DtoMapper.ToDto).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateModelRequest req)
    {
        var modelId = req.ModelId.Trim();
        if (await _db.AiModels.AnyAsync(m => m.ModelId == modelId))
            return Conflict(new { error = "Dieses Modell existiert bereits." });
        var model = new AiModel
        {
            ModelId = modelId,
            DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? modelId : req.DisplayName!,
            Provider = req.Provider ?? "",
            IsActive = req.IsActive,
            MaxContextTokens = req.MaxContextTokens,
            InputCostPer1M = req.InputCostPer1M,
            OutputCostPer1M = req.OutputCostPer1M
        };
        _db.AiModels.Add(model);
        _audit.Add(User.GetUserId(), AuditAction.Create, "AiModel", model.Id.ToString(), $"Modell '{modelId}' angelegt", Ip);
        await _db.SaveChangesAsync();
        return Ok(DtoMapper.ToDto(model));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateModelRequest req)
    {
        var model = await _db.AiModels.FirstOrDefaultAsync(m => m.Id == id);
        if (model == null) return NotFound(new { error = "Modell nicht gefunden." });
        model.ModelId = req.ModelId.Trim();
        model.DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? model.ModelId : req.DisplayName!;
        model.Provider = req.Provider ?? "";
        model.IsActive = req.IsActive;
        model.MaxContextTokens = req.MaxContextTokens;
        model.InputCostPer1M = req.InputCostPer1M;
        model.OutputCostPer1M = req.OutputCostPer1M;
        model.UpdatedAt = DateTime.UtcNow;
        _audit.Add(User.GetUserId(), AuditAction.Update, "AiModel", id.ToString(), $"Modell '{model.ModelId}' aktualisiert", Ip);
        await _db.SaveChangesAsync();
        return Ok(DtoMapper.ToDto(model));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var model = await _db.AiModels.FirstOrDefaultAsync(m => m.Id == id);
        if (model == null) return NotFound(new { error = "Modell nicht gefunden." });
        // Modelle werden deaktiviert statt gelöscht, damit Zuordnungen erhalten bleiben.
        model.IsActive = false;
        model.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Modell deaktiviert." });
    }

    /// <summary>Importiert die von LiteLLM gemeldeten Modelle.</summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
        if (!_llm.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "LiteLLM ist nicht konfiguriert." });
        IReadOnlyList<LiteLlmModelInfo> upstream;
        try
        {
            upstream = await _llm.ListModelsAsync(HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = $"Modelle konnten nicht geladen werden: {ex.Message}" });
        }

        int added = 0, updated = 0;
        var existing = await _db.AiModels.ToDictionaryAsync(m => m.ModelId);
        foreach (var info in upstream)
        {
            if (existing.TryGetValue(info.ModelId, out var model))
            {
                model.DisplayName = info.DisplayName;
                model.Provider = info.Provider;
                model.MaxContextTokens = info.MaxContextTokens ?? model.MaxContextTokens;
                model.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
            else
            {
                _db.AiModels.Add(new AiModel
                {
                    ModelId = info.ModelId,
                    DisplayName = info.DisplayName,
                    Provider = info.Provider,
                    MaxContextTokens = info.MaxContextTokens,
                    IsActive = true
                });
                added++;
            }
        }
        _audit.Add(User.GetUserId(), AuditAction.Update, "AiModel", null, $"LiteLLM-Sync: {added} neu, {updated} aktualisiert", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { added, updated, total = upstream.Count });
    }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
}
