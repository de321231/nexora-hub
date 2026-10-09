using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.Services;
using Nexora.Shared.Models;

namespace Nexora.Api.Controllers;

/// <summary>
/// Legacy-Endpunkt für registrierte Anwendungen (bleibt aus Kompatibilitätsgründen
/// erhalten). Lizenzprüfung erfolgt jetzt über <see cref="UserLicense"/>.
/// </summary>
[ApiController]
[Route("api/v1/apps")]
public class AppController : ControllerBase
{
    private readonly AppDbContext _db;
    public AppController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List()
        => Ok(await _db.AppDefinitions.AsNoTracking().Where(a => a.Status == AppStatus.Published).ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var app = await _db.AppDefinitions.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        return app == null ? NotFound() : Ok(app);
    }

    [HttpGet("{id:guid}/license-check")]
    [Authorize]
    public async Task<IActionResult> CheckLicense(Guid id)
    {
        var app = await _db.AppDefinitions.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (app == null || !app.LicenseRequired) return Ok(new { allowed = true });

        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var now = DateTime.UtcNow;
        var licenses = await (from ul in _db.UserLicenses
                              join l in _db.Licenses on ul.LicenseId equals l.Id
                              where ul.UserId == userId && ul.RevokedAt == null && l.ProductId == app.ProductId
                              select l).ToListAsync();

        var allowed = licenses.Any(l => LicenseEvaluator.IsUsable(l, now));
        return Ok(new { allowed });
    }
}
