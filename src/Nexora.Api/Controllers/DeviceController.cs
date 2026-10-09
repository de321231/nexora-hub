using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.Services;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Controllers;

[ApiController]
[Route("api/v1/devices")]
[Authorize]
public class DeviceController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IQuotaService _quota;
    private readonly IDeviceService _devices;
    private readonly IAuditService _audit;

    public DeviceController(AppDbContext db, IQuotaService quota, IDeviceService devices, IAuditService audit)
    {
        _db = db;
        _quota = quota;
        _devices = devices;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var rows = await (from a in _db.Activations
                          join d in _db.Devices on a.DeviceId equals d.Id
                          where d.UserId == userId
                          select new { a, d }).ToListAsync();

        var result = rows
            .GroupBy(r => r.d.Id)
            .Select(g =>
            {
                var first = g.First();
                var active = g.Any(x => x.a.RevokedAt == null);
                return DtoMapper.ToDto(first.d, first.a.LicenseId, !active);
            })
            .OrderByDescending(d => d.LastSeenAt)
            .ToList();
        return Ok(result);
    }

    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateDeviceRequest req)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        License? license = req.LicenseId.HasValue
            ? await _db.Licenses.Include(l => l.Product).FirstOrDefaultAsync(l => l.Id == req.LicenseId)
            : await _quota.ResolveActiveLicenseAsync(userId, null, default);

        if (license == null)
            return BadRequest(new { error = "Keine aktive Lizenz vorhanden." });

        // Zugehörigkeit prüfen.
        if (!await _db.UserLicenses.AnyAsync(ul => ul.UserId == userId && ul.LicenseId == license.Id && ul.RevokedAt == null))
            return Forbid();
        if (!LicenseEvaluator.IsUsable(license, DateTime.UtcNow))
            return BadRequest(new { error = "Die Lizenz ist nicht aktiv." });

        var result = await _devices.ActivateAsync(userId, license, req.DeviceIdentifier, req.DeviceName, req.Platform, default);
        if (!result.Ok) return BadRequest(new { error = result.Error });
        _audit.Add(userId, AuditAction.Activate, "Device", result.Device?.Id.ToString(), "Gerät aktiviert", HttpContext.Connection.RemoteIpAddress?.ToString());
        await _db.SaveChangesAsync();
        return Ok(result.Device);
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        var ok = await _devices.RevokeAsync(userId, id, default);
        if (!ok) return NotFound(new { error = "Gerät nicht gefunden." });
        _audit.Add(userId, AuditAction.Revoke, "Device", id.ToString(), "Gerät deaktiviert", HttpContext.Connection.RemoteIpAddress?.ToString());
        await _db.SaveChangesAsync();
        return Ok(new { message = "Gerät wurde deaktiviert." });
    }
}
