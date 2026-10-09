using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.Services;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Controllers;

[ApiController]
[Route("api/v1/licenses")]
[Authorize]
public class LicenseController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IQuotaService _quota;
    private readonly IDeviceService _devices;
    private readonly IAuditService _audit;

    public LicenseController(AppDbContext db, IQuotaService quota, IDeviceService devices, IAuditService audit)
    {
        _db = db;
        _quota = quota;
        _devices = devices;
        _audit = audit;
    }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>Weg 1: Lizenzschlüssel einlösen (transaktionssicher, keine Doppeleinlösung).</summary>
    [HttpPost("redeem")]
    public async Task<IActionResult> Redeem([FromBody] RedeemKeyRequest req)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var hash = LicenseKeyGenerator.Hash(req.Key);
        await using var tx = await _db.Database.BeginTransactionAsync();
        if (_db.Database.IsNpgsql())
            await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext({0})::bigint)", new object[] { "key:" + hash });

        var key = await _db.LicenseKeys
            .Include(k => k.License).ThenInclude(l => l!.Product)
            .FirstOrDefaultAsync(k => k.KeyHash == hash);

        if (key == null)
        {
            await tx.RollbackAsync();
            return BadRequest(new { error = "Lizenzschlüssel ist ungültig." });
        }

        var now = DateTime.UtcNow;

        if (key.Status == LicenseKeyStatus.Revoked)
        {
            await tx.RollbackAsync();
            return BadRequest(new { error = "Dieser Lizenzschlüssel wurde widerrufen." });
        }
        if (key.ExpiresAt.HasValue && key.ExpiresAt.Value <= now)
        {
            key.Status = LicenseKeyStatus.Expired;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return BadRequest(new { error = "Dieser Lizenzschlüssel ist abgelaufen." });
        }
        if (key.Status == LicenseKeyStatus.Redeemed && key.UsedCount >= key.MaxUses)
        {
            await tx.RollbackAsync();
            return BadRequest(new { error = "Dieser Lizenzschlüssel wurde bereits eingelöst." });
        }

        var license = key.License;
        if (license == null)
        {
            await tx.RollbackAsync();
            return BadRequest(new { error = "Der Schlüssel ist mit keiner Lizenz verbunden." });
        }

        LicenseEvaluator.RefreshStoredStatus(license, now);
        if (!LicenseEvaluator.IsUsable(license, now))
        {
            await tx.RollbackAsync();
            var reason = license.Status switch
            {
                LicenseStatus.Expired => "Die zugehörige Lizenz ist abgelaufen.",
                LicenseStatus.Suspended => "Die zugehörige Lizenz ist gesperrt.",
                LicenseStatus.Revoked => "Die zugehörige Lizenz wurde widerrufen.",
                LicenseStatus.Pending => "Die zugehörige Lizenz ist noch nicht aktiv.",
                _ => "Die zugehörige Lizenz ist nicht gültig."
            };
            return BadRequest(new { error = reason });
        }

        // Zuweisung an den Benutzer (Weg 1).
        var existingLink = await _db.UserLicenses.FirstOrDefaultAsync(ul => ul.UserId == userId && ul.LicenseId == license.Id);
        if (existingLink == null)
        {
            _db.UserLicenses.Add(new UserLicense
            {
                UserId = userId,
                LicenseId = license.Id,
                Source = "key",
                LicenseKeyId = key.Id,
                AssignedAt = now
            });
        }
        else if (existingLink.RevokedAt != null)
        {
            existingLink.RevokedAt = null;
        }

        key.UsedCount += 1;
        key.RedeemedByUserId = userId;
        key.RedeemedAt = now;
        if (key.UsedCount >= key.MaxUses) key.Status = LicenseKeyStatus.Redeemed;
        if (license.Status == LicenseStatus.Pending && license.StartsAt == null) { license.StartsAt = now; license.Status = LicenseStatus.Active; }

        _audit.Add(userId, AuditAction.RedeemKey, "LicenseKey", key.Id.ToString(), $"Schlüssel eingelöst für Lizenz {license.Id}", Ip);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        DeviceDto? device = null;
        if (!string.IsNullOrWhiteSpace(req.DeviceIdentifier))
        {
            var res = await _devices.ActivateAsync(userId, license, req.DeviceIdentifier!, req.DeviceName, null, default);
            if (res.Ok) device = res.Device;
        }

        await _db.Entry(license).Reference(l => l.Product).LoadAsync();
        var licenseModels = await _db.LicenseModels.Include(lm => lm.AiModel).Where(lm => lm.LicenseId == license.Id).ToListAsync();
        var activeDevices = await _devices.CountActiveAsync(license.Id, default);
        var quota = await _quota.BuildStatusAsync(userId, license, default);
        return Ok(new { license = DtoMapper.ToDto(license, licenseModels, activeDevices), device, quota });
    }

    /// <summary>Alle Lizenzen des angemeldeten Benutzers.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var licenses = await (from ul in _db.UserLicenses
                              join l in _db.Licenses.Include(x => x.Product) on ul.LicenseId equals l.Id
                              where ul.UserId == userId && ul.RevokedAt == null
                              select l).Distinct().ToListAsync();

        var now = DateTime.UtcNow;
        var changed = false;
        foreach (var l in licenses) changed |= LicenseEvaluator.RefreshStoredStatus(l, now);
        if (changed) await _db.SaveChangesAsync();

        var ids = licenses.Select(l => l.Id).ToList();
        var models = await _db.LicenseModels.Include(lm => lm.AiModel).Where(lm => ids.Contains(lm.LicenseId)).ToListAsync();
        var deviceCounts = await _db.Activations.Where(a => a.RevokedAt == null && ids.Contains(a.LicenseId))
            .GroupBy(a => a.LicenseId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

        var result = licenses.Select(l => DtoMapper.ToDto(l, models.Where(m => m.LicenseId == l.Id),
            deviceCounts.TryGetValue(l.Id, out var c) ? c : 0)).ToList();
        return Ok(result);
    }

    [HttpGet("{id:guid}/quota")]
    public async Task<IActionResult> Quota(Guid id)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        var license = await GetOwnedLicenseAsync(userId, id);
        if (license == null) return NotFound(new { error = "Lizenz nicht gefunden." });
        return Ok(await _quota.BuildStatusAsync(userId, license, default));
    }

    [HttpGet("{id:guid}/usage")]
    public async Task<IActionResult> Usage(Guid id, [FromQuery] int take = 50)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        var license = await GetOwnedLicenseAsync(userId, id);
        if (license == null) return NotFound(new { error = "Lizenz nicht gefunden." });
        take = Math.Clamp(take, 1, 200);
        var events = await _db.UsageEvents.AsNoTracking()
            .Where(e => e.UserId == userId && e.LicenseId == id)
            .OrderByDescending(e => e.CreatedAtUtc).Take(take)
            .Select(e => new UsageEventDto
            {
                Id = e.Id,
                ModelId = e.ModelId,
                InputTokens = e.InputTokens,
                OutputTokens = e.OutputTokens,
                TotalTokens = e.TotalTokens,
                TokensEstimated = e.TokensEstimated,
                Success = e.Success,
                ErrorCode = e.ErrorCode,
                LatencyMs = e.LatencyMs,
                CostUsd = e.CostUsd,
                CreatedAtUtc = e.CreatedAtUtc
            }).ToListAsync();
        return Ok(events);
    }

    private async Task<License?> GetOwnedLicenseAsync(string userId, Guid licenseId)
    {
        var owned = await _db.UserLicenses.AnyAsync(ul => ul.UserId == userId && ul.LicenseId == licenseId && ul.RevokedAt == null);
        if (!owned) return null;
        return await _db.Licenses.Include(l => l.Product).FirstOrDefaultAsync(l => l.Id == licenseId);
    }
}
