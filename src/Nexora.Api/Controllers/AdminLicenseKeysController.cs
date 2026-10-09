using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.Services;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Controllers;

[ApiController]
[Route("api/v1/admin/license-keys")]
[Authorize(Policy = "Admin")]
public class AdminLicenseKeysController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public AdminLicenseKeysController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? licenseId, [FromQuery] LicenseKeyStatus? status)
    {
        var query = _db.LicenseKeys.Include(k => k.License).AsNoTracking().AsQueryable();
        if (licenseId.HasValue) query = query.Where(k => k.LicenseId == licenseId.Value);
        if (status.HasValue) query = query.Where(k => k.Status == status.Value);

        var keys = await query.OrderByDescending(k => k.CreatedAt).Take(1000).ToListAsync();
        var userIds = keys.Where(k => k.RedeemedByUserId != null).Select(k => k.RedeemedByUserId!).Distinct().ToList();
        var emails = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email);

        return Ok(keys.Select(k => ToDto(k,
            k.RedeemedByUserId != null && emails.TryGetValue(k.RedeemedByUserId, out var e) ? e : null)).ToList());
    }

    /// <summary>Erzeugt neue Lizenzschlüssel. Der Klartext wird genau einmal zurückgegeben.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLicenseKeyRequest req)
    {
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.Id == req.LicenseId);
        if (license == null) return BadRequest(new { error = "Lizenz nicht gefunden." });

        var count = Math.Clamp(req.Count, 1, 500);
        var maxUses = Math.Max(1, req.MaxUses);
        var now = DateTime.UtcNow;

        var created = new List<CreatedLicenseKeyDto>();
        for (int i = 0; i < count; i++)
        {
            var (plain, hash, prefix) = LicenseKeyGenerator.Generate(license.KeyPrefix);
            var key = new LicenseKey
            {
                LicenseId = license.Id,
                KeyHash = hash,
                KeyPrefix = prefix,
                Status = LicenseKeyStatus.Available,
                CreatedAt = now,
                ExpiresAt = req.ExpiresAt,
                MaxUses = maxUses,
                Note = req.Note
            };
            _db.LicenseKeys.Add(key);
            created.Add(new CreatedLicenseKeyDto
            {
                Id = key.Id,
                LicenseId = license.Id,
                Key = plain,
                KeyPrefix = prefix,
                ExpiresAt = req.ExpiresAt
            });
        }

        _audit.Add(User.GetUserId(), AuditAction.Create, "LicenseKey", license.Id.ToString(),
            $"{count} Lizenzschlüssel für Lizenz {license.Id} erzeugt", Ip);
        await _db.SaveChangesAsync();

        return Ok(created);
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var key = await _db.LicenseKeys.FirstOrDefaultAsync(k => k.Id == id);
        if (key == null) return NotFound(new { error = "Lizenzschlüssel nicht gefunden." });
        key.Status = LicenseKeyStatus.Revoked;
        key.RevokedAt = DateTime.UtcNow;
        _audit.Add(User.GetUserId(), AuditAction.Revoke, "LicenseKey", id.ToString(), "Lizenzschlüssel widerrufen", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Lizenzschlüssel widerrufen." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var key = await _db.LicenseKeys.FirstOrDefaultAsync(k => k.Id == id);
        if (key == null) return NotFound(new { error = "Lizenzschlüssel nicht gefunden." });
        if (key.Status == LicenseKeyStatus.Redeemed)
            return Conflict(new { error = "Eingelöste Schlüssel können nicht gelöscht, aber widerrufen werden." });
        _db.LicenseKeys.Remove(key);
        _audit.Add(User.GetUserId(), AuditAction.Delete, "LicenseKey", id.ToString(), "Lizenzschlüssel gelöscht", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Lizenzschlüssel gelöscht." });
    }

    private static LicenseKeyDto ToDto(LicenseKey k, string? email = null) => new()
    {
        Id = k.Id,
        LicenseId = k.LicenseId,
        LicenseName = k.License?.Name,
        KeyPrefix = k.KeyPrefix,
        Status = k.Status,
        CreatedAt = k.CreatedAt,
        ExpiresAt = k.ExpiresAt,
        MaxUses = k.MaxUses,
        UsedCount = k.UsedCount,
        RedeemedByUserId = k.RedeemedByUserId,
        RedeemedByEmail = email,
        RedeemedAt = k.RedeemedAt
    };
}
