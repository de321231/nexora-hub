using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.Services;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Controllers;

[ApiController]
[Route("api/v1/admin/apps")]
[Authorize(Policy = "Admin")]
public class AdminAppsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    public AdminAppsController(AppDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var apps = await _db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        return Ok(apps.Select(DtoMapper.ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var app = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return app == null ? NotFound(new { error = "App nicht gefunden." }) : Ok(DtoMapper.ToDto(app));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppRequest req)
    {
        var slug = string.IsNullOrWhiteSpace(req.Slug) ? Slugify(req.Name) : Slugify(req.Slug!);
        if (await _db.Products.AnyAsync(p => p.Slug == slug))
            slug = $"{slug}-{Guid.NewGuid().ToString("N")[..4]}";

        var app = new Product
        {
            Name = req.Name.Trim(),
            Slug = slug,
            Description = req.Description ?? "",
            Kind = req.Kind,
            Status = req.Status,
            LicenseRequired = req.LicenseRequired,
            IsActive = true
        };
        _db.Products.Add(app);
        _audit.Add(User.GetUserId(), AuditAction.Create, "App", app.Id.ToString(), $"App '{app.Name}' erstellt", Ip);
        await _db.SaveChangesAsync();
        return Ok(DtoMapper.ToDto(app));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppRequest req)
    {
        var app = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (app == null) return NotFound(new { error = "App nicht gefunden." });
        app.Name = req.Name.Trim();
        app.Description = req.Description ?? "";
        app.Kind = req.Kind;
        app.Status = req.Status;
        app.LicenseRequired = req.LicenseRequired;
        app.IsActive = req.IsActive;
        app.UpdatedAt = DateTime.UtcNow;
        _audit.Add(User.GetUserId(), AuditAction.Update, "App", id.ToString(), $"App '{app.Name}' aktualisiert", Ip);
        await _db.SaveChangesAsync();
        return Ok(DtoMapper.ToDto(app));
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id)
    {
        var app = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (app == null) return NotFound(new { error = "App nicht gefunden." });
        app.Status = AppStatus.Published;
        app.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(DtoMapper.ToDto(app));
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id)
    {
        var app = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (app == null) return NotFound(new { error = "App nicht gefunden." });
        app.Status = AppStatus.Archived;
        app.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(DtoMapper.ToDto(app));
    }

    private static string Slugify(string value)
    {
        var slug = value.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, "[äöüß]", m => m.Value switch { "ä" => "ae", "ö" => "oe", "ü" => "ue", "ß" => "ss", _ => "" });
        slug = Regex.Replace(slug, "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "app" : slug[..Math.Min(slug.Length, 80)];
    }
}
