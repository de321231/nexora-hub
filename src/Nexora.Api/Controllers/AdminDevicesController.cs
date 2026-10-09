using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.Services;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Controllers;

[ApiController]
[Route("api/v1/admin/devices")]
[Authorize(Policy = "Admin")]
public class AdminDevicesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public AdminDevicesController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search)
    {
        var query = _db.Activations.AsNoTracking()
            .Include(a => a.Device)
            .Include(a => a.License)!
            .ThenInclude(l => l!.Product)
            .AsQueryable();

        // Alle Geräte gruppiert nach Geräte-ID; eine Aktivierung pro Gruppe für den Zustand.
        var rows = await query.ToListAsync();
        var grouped = rows.GroupBy(r => r.DeviceId).ToList();

        var userIds = grouped.Select(g => g.First().Device?.UserId)
            .Where(u => u != null).Select(u => u!).Distinct().ToList();
        var emails = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email);

        var result = new List<AdminDeviceDto>();
        foreach (var g in grouped)
        {
            var first = g.First();
            var d = first.Device;
            if (d == null) continue;
            var active = g.Any(x => x.RevokedAt == null);
            var licenseIds = g.Select(x => x.LicenseId).Distinct().ToList();
            var licenseNames = rows
                .Where(r => licenseIds.Contains(r.LicenseId) && r.License != null)
                .Select(r => r.License!.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct().ToList();

            var dto = new AdminDeviceDto
            {
                Id = d.Id,
                DeviceName = d.DeviceName,
                DeviceIdentifier = d.DeviceIdentifier,
                Platform = d.Platform,
                UserId = d.UserId,
                UserEmail = d.UserId != null && emails.TryGetValue(d.UserId, out var mail) ? mail : null,
                LicenseId = first.LicenseId,
                LicenseName = licenseNames.Count > 0 ? string.Join(", ", licenseNames) : null,
                AppName = first.License?.Product?.Name ?? "Nexora AI",
                Revoked = !active,
                DeviceActive = active,
                ActivatedAt = active
                    ? g.Where(x => x.RevokedAt == null).Min(x => x.ActivatedAt)
                    : first.ActivatedAt,
                LastValidatedAt = active
                    ? g.Where(x => x.RevokedAt == null).Max(x => x.LastValidatedAt)
                    : first.LastValidatedAt,
                FirstSeenAt = d.FirstSeenAt,
                LastSeenAt = d.LastSeenAt
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                var match = (dto.DeviceName ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                            || (dto.DeviceIdentifier ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)
                            || (dto.UserEmail ?? "").Contains(s, StringComparison.OrdinalIgnoreCase);
                if (!match) continue;
            }
            result.Add(dto);
        }

        return Ok(result.OrderByDescending(d => d.LastSeenAt).ToList());
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var activations = await _db.Activations.Where(a => a.DeviceId == id && a.RevokedAt == null).ToListAsync();
        if (activations.Count == 0)
            return NotFound(new { error = "Gerät hat keine aktiven Aktivierungen." });
        foreach (var a in activations) a.RevokedAt = DateTime.UtcNow;
        _audit.Add(User.GetUserId(), AuditAction.Revoke, "Device", id.ToString(), "Gerät vom Admin widerrufen", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Gerät wurde widerrufen." });
    }
}