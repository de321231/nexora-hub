using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.Services;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Controllers;

[ApiController]
[Route("api/v1/admin/licenses")]
[Authorize(Policy = "Admin")]
public class AdminLicensesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    public AdminLicensesController(AppDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? appId, [FromQuery] LicenseStatus? status, [FromQuery] string? search)
    {
        var query = _db.Licenses.Include(l => l.Product).AsNoTracking().AsQueryable();
        if (appId.HasValue) query = query.Where(l => l.ProductId == appId.Value);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(l => l.Name != null && EF.Functions.ILike(l.Name, $"%{search}%"));
        var licenses = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();

        var now = DateTime.UtcNow;
        var dtos = await BuildAdminDtosAsync(licenses, now);
        if (status.HasValue) dtos = dtos.Where(d => d.Status == status.Value).ToList();
        return Ok(dtos);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var license = await _db.Licenses.Include(l => l.Product).FirstOrDefaultAsync(l => l.Id == id);
        if (license == null) return NotFound(new { error = "Lizenz nicht gefunden." });
        var dto = (await BuildAdminDtosAsync(new List<License> { license }, DateTime.UtcNow)).First();
        var keys = await _db.LicenseKeys.AsNoTracking().Where(k => k.LicenseId == id).OrderByDescending(k => k.CreatedAt).ToListAsync();
        var users = await (from ul in _db.UserLicenses
                           join u in _db.Users on ul.UserId equals u.Id
                           where ul.LicenseId == id
                           select new { u.Id, u.Email, u.FullName, ul.AssignedAt, Revoked = ul.RevokedAt != null }).ToListAsync();
        return Ok(new { license = dto, keys = keys.Select(k => ToKeyDto(k)).ToList(), users });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLicenseRequest req)
    {
        var app = await _db.Products.FirstOrDefaultAsync(p => p.Id == req.AppId);
        if (app == null) return BadRequest(new { error = "App nicht gefunden." });

        var license = new License
        {
            ProductId = req.AppId,
            Name = req.Name,
            KeyPrefix = string.IsNullOrWhiteSpace(req.KeyPrefix) ? "NX" : req.KeyPrefix!.Trim().ToUpperInvariant(),
            StartsAt = req.StartsAt,
            ExpiresAt = req.ExpiresAt,
            MaxDevices = Math.Max(1, req.MaxDevices),
            MaxMessagesPerDay = req.MaxMessagesPerDay,
            MaxMessagesPerMonth = req.MaxMessagesPerMonth,
            MaxInputTokensPerDay = req.MaxInputTokensPerDay,
            MaxOutputTokensPerDay = req.MaxOutputTokensPerDay,
            MaxTotalTokensPerDay = req.MaxTotalTokensPerDay,
            MaxInputTokensPerMonth = req.MaxInputTokensPerMonth,
            MaxOutputTokensPerMonth = req.MaxOutputTokensPerMonth,
            MaxTotalTokensPerMonth = req.MaxTotalTokensPerMonth,
            MaxTokensPerRequest = req.MaxTokensPerRequest,
            AdminNotes = req.AdminNotes
        };
        LicenseEvaluator.RefreshStoredStatus(license, DateTime.UtcNow);
        _db.Licenses.Add(license);
        await SetModelsAsync(license.Id, req.ModelIds);
        _audit.Add(User.GetUserId(), AuditAction.Create, "License", license.Id.ToString(), $"Lizenz '{license.Name ?? license.Id.ToString()}' für {app.Name} erstellt", Ip);
        await _db.SaveChangesAsync();

        await _db.Entry(license).Reference(l => l.Product).LoadAsync();
        return Ok((await BuildAdminDtosAsync(new List<License> { license }, DateTime.UtcNow)).First());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLicenseRequest req)
    {
        var license = await _db.Licenses.Include(l => l.Product).FirstOrDefaultAsync(l => l.Id == id);
        if (license == null) return NotFound(new { error = "Lizenz nicht gefunden." });

        license.Name = req.Name;
        license.Status = req.Status;
        license.StartsAt = req.StartsAt;
        license.ExpiresAt = req.ExpiresAt;
        license.MaxDevices = Math.Max(1, req.MaxDevices);
        license.MaxMessagesPerDay = req.MaxMessagesPerDay;
        license.MaxMessagesPerMonth = req.MaxMessagesPerMonth;
        license.MaxInputTokensPerDay = req.MaxInputTokensPerDay;
        license.MaxOutputTokensPerDay = req.MaxOutputTokensPerDay;
        license.MaxTotalTokensPerDay = req.MaxTotalTokensPerDay;
        license.MaxInputTokensPerMonth = req.MaxInputTokensPerMonth;
        license.MaxOutputTokensPerMonth = req.MaxOutputTokensPerMonth;
        license.MaxTotalTokensPerMonth = req.MaxTotalTokensPerMonth;
        license.MaxTokensPerRequest = req.MaxTokensPerRequest;
        license.AdminNotes = req.AdminNotes;
        license.UpdatedAt = DateTime.UtcNow;
        if (req.Status is not (LicenseStatus.Suspended or LicenseStatus.Revoked))
            LicenseEvaluator.RefreshStoredStatus(license, DateTime.UtcNow);

        await SetModelsAsync(license.Id, req.ModelIds);
        _audit.Add(User.GetUserId(), AuditAction.Update, "License", id.ToString(), "Lizenz aktualisiert", Ip);
        await _db.SaveChangesAsync();
        return Ok((await BuildAdminDtosAsync(new List<License> { license }, DateTime.UtcNow)).First());
    }

    [HttpPost("{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id) => await SetStatus(id, LicenseStatus.Suspended);

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id) => await SetStatus(id, LicenseStatus.Active);

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.Id == id);
        if (license == null) return NotFound(new { error = "Lizenz nicht gefunden." });
        license.Status = LicenseStatus.Revoked;
        license.RevokedAt = DateTime.UtcNow;
        license.UpdatedAt = DateTime.UtcNow;
        _audit.Add(User.GetUserId(), AuditAction.Revoke, "License", id.ToString(), "Lizenz widerrufen", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Lizenz widerrufen." });
    }

    [HttpPost("{id:guid}/extend")]
    public async Task<IActionResult> Extend(Guid id, [FromBody] ExtendRequest req)
    {
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.Id == id);
        if (license == null) return NotFound(new { error = "Lizenz nicht gefunden." });
        var baseDate = license.ExpiresAt.HasValue && license.ExpiresAt.Value > DateTime.UtcNow ? license.ExpiresAt.Value : DateTime.UtcNow;
        license.ExpiresAt = baseDate.AddDays(Math.Max(1, req.Days));
        if (license.Status == LicenseStatus.Expired) LicenseEvaluator.RefreshStoredStatus(license, DateTime.UtcNow);
        license.UpdatedAt = DateTime.UtcNow;
        _audit.Add(User.GetUserId(), AuditAction.Update, "License", id.ToString(), $"Lizenz um {req.Days} Tage verlängert", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { expiresAt = license.ExpiresAt });
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignUserRequest req)
    {
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.Id == id);
        if (license == null) return NotFound(new { error = "Lizenz nicht gefunden." });
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId);
        if (user == null) return NotFound(new { error = "Benutzer nicht gefunden." });

        var now = DateTime.UtcNow;
        var existing = await _db.UserLicenses.FirstOrDefaultAsync(ul => ul.UserId == req.UserId && ul.LicenseId == id);
        if (existing == null)
            _db.UserLicenses.Add(new UserLicense { UserId = req.UserId, LicenseId = id, Source = "admin", AssignedAt = now, AssignedByUserId = User.GetUserId() });
        else if (existing.RevokedAt != null)
        {
            existing.RevokedAt = null;
            existing.AssignedAt = now;
            existing.AssignedByUserId = User.GetUserId();
        }
        else return Conflict(new { error = "Lizenz ist bereits zugewiesen." });

        LicenseEvaluator.RefreshStoredStatus(license, now);
        if (license.Status == LicenseStatus.Pending && license.StartsAt == null) { license.StartsAt = now; license.Status = LicenseStatus.Active; }
        _audit.Add(User.GetUserId(), AuditAction.LicenseAssigned, "License", id.ToString(), $"Lizenz an {user.Email} zugewiesen", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Lizenz zugewiesen." });
    }

    [HttpPost("{id:guid}/unassign")]
    public async Task<IActionResult> Unassign(Guid id, [FromBody] AssignUserRequest req)
    {
        var link = await _db.UserLicenses.FirstOrDefaultAsync(ul => ul.UserId == req.UserId && ul.LicenseId == id && ul.RevokedAt == null);
        if (link == null) return NotFound(new { error = "Zuweisung nicht gefunden." });
        link.RevokedAt = DateTime.UtcNow;
        _audit.Add(User.GetUserId(), AuditAction.LicenseRemoved, "License", id.ToString(), $"Lizenz von {req.UserId} entfernt", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Zuweisung entfernt." });
    }

    [HttpPut("{id:guid}/models")]
    public async Task<IActionResult> SetModels(Guid id, [FromBody] List<Guid> modelIds)
    {
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.Id == id);
        if (license == null) return NotFound(new { error = "Lizenz nicht gefunden." });
        await SetModelsAsync(id, modelIds ?? new List<Guid>());
        _audit.Add(User.GetUserId(), AuditAction.Update, "License", id.ToString(), $"{modelIds?.Count ?? 0} Modell(e) zugewiesen", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Modelle aktualisiert." });
    }

    [HttpGet("{id:guid}/keys")]
    public async Task<IActionResult> Keys(Guid id)
    {
        var keys = await _db.LicenseKeys.AsNoTracking().Where(k => k.LicenseId == id).OrderByDescending(k => k.CreatedAt).ToListAsync();
        var userIds = keys.Where(k => k.RedeemedByUserId != null).Select(k => k.RedeemedByUserId!).Distinct().ToList();
        var emails = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email);
        return Ok(keys.Select(k => ToKeyDto(k, k.RedeemedByUserId != null && emails.TryGetValue(k.RedeemedByUserId, out var e) ? e : null)).ToList());
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id)
    {
        var logs = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityId == id.ToString() && (a.EntityType == "License" || a.EntityType == "LicenseKey"))
            .OrderByDescending(a => a.Timestamp).Take(100).ToListAsync();
        return Ok(logs);
    }

    private async Task<IActionResult> SetStatus(Guid id, LicenseStatus status)
    {
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.Id == id);
        if (license == null) return NotFound(new { error = "Lizenz nicht gefunden." });
        license.Status = status;
        if (status != LicenseStatus.Revoked) license.RevokedAt = null;
        license.UpdatedAt = DateTime.UtcNow;
        if (status is not (LicenseStatus.Suspended or LicenseStatus.Revoked))
            LicenseEvaluator.RefreshStoredStatus(license, DateTime.UtcNow);
        _audit.Add(User.GetUserId(), AuditAction.Update, "License", id.ToString(), $"Status gesetzt: {status}", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { status = license.Status });
    }

    private async Task SetModelsAsync(Guid licenseId, List<Guid> modelIds)
    {
        var existing = await _db.LicenseModels.Where(lm => lm.LicenseId == licenseId).ToListAsync();
        _db.LicenseModels.RemoveRange(existing);
        var valid = await _db.AiModels.Where(m => modelIds.Contains(m.Id)).Select(m => m.Id).ToListAsync();
        foreach (var mid in valid.Distinct())
            _db.LicenseModels.Add(new LicenseModel { LicenseId = licenseId, AiModelId = mid });
    }

    private async Task<List<AdminLicenseDto>> BuildAdminDtosAsync(List<License> licenses, DateTime now)
    {
        var ids = licenses.Select(l => l.Id).ToList();
        var models = await _db.LicenseModels.Include(lm => lm.AiModel).Where(lm => ids.Contains(lm.LicenseId)).ToListAsync();
        var keyCounts = await _db.LicenseKeys.Where(k => ids.Contains(k.LicenseId)).GroupBy(k => k.LicenseId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var userLicenses = await (from ul in _db.UserLicenses
                                  join u in _db.Users on ul.UserId equals u.Id
                                  where ids.Contains(ul.LicenseId)
                                  select new { ul.LicenseId, u.Email, Revoked = ul.RevokedAt != null }).ToListAsync();
        var deviceCounts = await _db.Activations.Where(a => a.RevokedAt == null && ids.Contains(a.LicenseId))
            .GroupBy(a => a.LicenseId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

        var result = new List<AdminLicenseDto>();
        foreach (var l in licenses)
        {
            LicenseEvaluator.RefreshStoredStatus(l, now);
            var baseDto = DtoMapper.ToDto(l, models.Where(m => m.LicenseId == l.Id),
                deviceCounts.TryGetValue(l.Id, out var dc) ? dc : 0);
            var users = userLicenses.Where(u => u.LicenseId == l.Id).ToList();
            result.Add(new AdminLicenseDto
            {
                Id = baseDto.Id,
                Name = baseDto.Name,
                AppId = baseDto.AppId,
                AppName = baseDto.AppName,
                AppSlug = baseDto.AppSlug,
                Status = baseDto.Status,
                StartsAt = baseDto.StartsAt,
                ExpiresAt = baseDto.ExpiresAt,
                MaxDevices = baseDto.MaxDevices,
                ActiveDevices = baseDto.ActiveDevices,
                MaxMessagesPerDay = baseDto.MaxMessagesPerDay,
                MaxMessagesPerMonth = baseDto.MaxMessagesPerMonth,
                MaxInputTokensPerDay = baseDto.MaxInputTokensPerDay,
                MaxOutputTokensPerDay = baseDto.MaxOutputTokensPerDay,
                MaxTotalTokensPerDay = baseDto.MaxTotalTokensPerDay,
                MaxInputTokensPerMonth = baseDto.MaxInputTokensPerMonth,
                MaxOutputTokensPerMonth = baseDto.MaxOutputTokensPerMonth,
                MaxTotalTokensPerMonth = baseDto.MaxTotalTokensPerMonth,
                MaxTokensPerRequest = baseDto.MaxTokensPerRequest,
                Models = baseDto.Models,
                KeyCount = keyCounts.TryGetValue(l.Id, out var kc) ? kc : 0,
                AssignedUserCount = users.Count(u => !u.Revoked),
                AssignedUserEmails = users.Where(u => !u.Revoked).Select(u => u.Email ?? "").ToList(),
                CreatedAt = l.CreatedAt
            });
        }
        return result;
    }

    private static LicenseKeyDto ToKeyDto(LicenseKey k, string? email = null) => new()
    {
        Id = k.Id,
        LicenseId = k.LicenseId,
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

    public class ExtendRequest { public int Days { get; set; } = 30; }
    public class AssignUserRequest { public string UserId { get; set; } = ""; }
}
