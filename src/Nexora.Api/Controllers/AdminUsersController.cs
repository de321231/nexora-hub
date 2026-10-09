using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.Services;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;

    public AdminUsersController(AppDbContext db, UserManager<ApplicationUser> userManager, IAuditService audit)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
    }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u => (u.Email != null && EF.Functions.ILike(u.Email, $"%{s}%"))
                                  || (u.FullName != null && EF.Functions.ILike(u.FullName, $"%{s}%")));
        }
        var users = await query.OrderByDescending(u => u.CreatedAt).Take(500).ToListAsync();
        var licenseCounts = await _db.UserLicenses.Where(ul => ul.RevokedAt == null)
            .GroupBy(ul => ul.UserId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var result = new List<UserDto>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            result.Add(DtoMapper.ToDto(u, roles, licenseCounts.TryGetValue(u.Id, out var c) ? c : 0));
        }
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { error = "Benutzer nicht gefunden." });
        var roles = await _userManager.GetRolesAsync(user);

        var licenses = await (from ul in _db.UserLicenses
                              join l in _db.Licenses.Include(x => x.Product) on ul.LicenseId equals l.Id
                              where ul.UserId == id
                              select new { ul, l }).ToListAsync();

        var licenseDtos = licenses.Select(x => new
        {
            x.ul.Id,
            x.ul.LicenseId,
            x.l.Name,
            AppName = x.l.Product?.Name,
            x.l.Status,
            x.l.ExpiresAt,
            Revoked = x.ul.RevokedAt != null
        }).ToList();

        return Ok(new { user = DtoMapper.ToDto(user, roles, licenses.Count(l => l.ul.RevokedAt == null)), licenses = licenseDtos });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest req)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { error = "Benutzer nicht gefunden." });

        user.FullName = req.FullName;
        user.IsActive = req.IsActive;
        user.IsLocked = req.IsLocked;
        await _userManager.UpdateAsync(user);
        _audit.Add(User.GetUserId(), AuditAction.Update, "User", id, $"Benutzer aktualisiert (aktiv={req.IsActive}, gesperrt={req.IsLocked})", Ip);
        await _db.SaveChangesAsync();
        var roles = await _userManager.GetRolesAsync(user);
        return Ok(DtoMapper.ToDto(user, roles));
    }

    [HttpPost("{id}/roles")]
    public async Task<IActionResult> SetRoles(string id, [FromBody] SetUserRolesRequest req)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { error = "Benutzer nicht gefunden." });

        var allowed = new[] { DatabaseSeeder.AdminRole, DatabaseSeeder.UserRole };
        var target = req.Roles.Where(r => allowed.Contains(r)).Distinct().ToList();
        if (target.Count == 0) target.Add(DatabaseSeeder.UserRole);

        var current = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, current.Except(target));
        await _userManager.AddToRolesAsync(user, target.Except(current));

        _audit.Add(User.GetUserId(), AuditAction.Update, "User", id, $"Rollen: {string.Join(", ", target)}", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { roles = target });
    }

    [HttpPost("{id}/assign-license")]
    public async Task<IActionResult> AssignLicense(string id, [FromBody] AssignLicenseRequest req)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound(new { error = "Benutzer nicht gefunden." });
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.Id == req.LicenseId);
        if (license == null) return NotFound(new { error = "Lizenz nicht gefunden." });

        var now = DateTime.UtcNow;
        var existing = await _db.UserLicenses.FirstOrDefaultAsync(ul => ul.UserId == id && ul.LicenseId == license.Id);
        if (existing != null)
        {
            if (existing.RevokedAt == null)
                return Conflict(new { error = "Diese Lizenz ist dem Benutzer bereits zugewiesen." });
            existing.RevokedAt = null;
            existing.AssignedAt = now;
            existing.AssignedByUserId = User.GetUserId();
        }
        else
        {
            _db.UserLicenses.Add(new UserLicense
            {
                UserId = id,
                LicenseId = license.Id,
                Source = "admin",
                AssignedAt = now,
                AssignedByUserId = User.GetUserId()
            });
        }

        // Direkte Zuweisung aktiviert eine ausstehende Lizenz.
        LicenseEvaluator.RefreshStoredStatus(license, now);
        if (license.Status == LicenseStatus.Pending && license.StartsAt == null)
        {
            license.StartsAt = now;
            license.Status = LicenseStatus.Active;
            license.UpdatedAt = now;
        }

        _audit.Add(User.GetUserId(), AuditAction.LicenseAssigned, "License", license.Id.ToString(), $"Lizenz an {user.Email} zugewiesen", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Lizenz zugewiesen." });
    }

    [HttpPost("{id}/remove-license/{licenseId:guid}")]
    public async Task<IActionResult> RemoveLicense(string id, Guid licenseId)
    {
        var link = await _db.UserLicenses.FirstOrDefaultAsync(ul => ul.UserId == id && ul.LicenseId == licenseId && ul.RevokedAt == null);
        if (link == null) return NotFound(new { error = "Zuweisung nicht gefunden." });
        link.RevokedAt = DateTime.UtcNow;
        _audit.Add(User.GetUserId(), AuditAction.LicenseRemoved, "License", licenseId.ToString(), $"Lizenz von {id} entfernt", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Lizenzzuweisung entfernt." });
    }

    [HttpGet("{id}/devices")]
    public async Task<IActionResult> Devices(string id)
    {
        var rows = await (from a in _db.Activations
                          join d in _db.Devices on a.DeviceId equals d.Id
                          where d.UserId == id
                          select new { a, d }).ToListAsync();
        var result = rows.GroupBy(r => r.d.Id).Select(g =>
        {
            var first = g.First();
            var active = g.Any(x => x.a.RevokedAt == null);
            return DtoMapper.ToDto(first.d, first.a.LicenseId, !active);
        }).ToList();
        return Ok(result);
    }

    [HttpPost("{id}/devices/{deviceId:guid}/revoke")]
    public async Task<IActionResult> RevokeDevice(string id, Guid deviceId)
    {
        var activations = await _db.Activations.Where(a => a.DeviceId == deviceId && a.RevokedAt == null).ToListAsync();
        foreach (var a in activations) a.RevokedAt = DateTime.UtcNow;
        _audit.Add(User.GetUserId(), AuditAction.Revoke, "Device", deviceId.ToString(), $"Gerät von {id} widerrufen", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Gerät widerrufen." });
    }

    [HttpGet("{id}/usage")]
    public async Task<IActionResult> Usage(string id, [FromQuery] int take = 100)
    {
        take = Math.Clamp(take, 1, 500);
        var user = await _userManager.FindByIdAsync(id);
        var events = await _db.UsageEvents.AsNoTracking()
            .Where(e => e.UserId == id)
            .OrderByDescending(e => e.CreatedAtUtc).Take(take)
            .Select(e => new AdminUsageEventDto
            {
                Id = e.Id,
                UserId = e.UserId,
                UserEmail = user != null ? user.Email : null,
                LicenseId = e.LicenseId,
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
}
