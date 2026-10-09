using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexora.Api.Data;
using Nexora.Api.Options;
using Nexora.Api.Services;
using Nexora.Shared.Models;

namespace Nexora.Api.Controllers;

/// <summary>
/// Einmaliger, kontrollierter Bootstrap des ersten Administrators.
/// Sobald ein Administrator existiert, ist dieser Endpunkt dauerhaft gesperrt.
/// </summary>
[ApiController]
[Route("api/v1/admin/setup")]
public class AdminSetupController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly BootstrapOptions _bootstrap;
    private readonly IHostEnvironment _env;

    public AdminSetupController(AppDbContext db, UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager, IOptions<BootstrapOptions> bootstrap, IHostEnvironment env)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _bootstrap = bootstrap.Value;
        _env = env;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == DatabaseSeeder.AdminRole);
        var hasAdmin = role != null && await _db.UserRoles.AnyAsync(ur => ur.RoleId == role.Id);
        return Ok(new { needsSetup = !hasAdmin });
    }

    [HttpPost]
    public async Task<IActionResult> CreateAdmin([FromBody] SetupRequest req)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == DatabaseSeeder.AdminRole);
        if (role != null && await _db.UserRoles.AnyAsync(ur => ur.RoleId == role.Id))
            return Conflict(new { error = "Ein Administrator existiert bereits. Der Bootstrap ist gesperrt." });

        if (!IsAllowedBootstrap())
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Bootstrap nicht erlaubt. Zugriff nur lokal oder mit gültigem Bootstrap-Geheimnis." });

        if (role == null)
        {
            role = new IdentityRole(DatabaseSeeder.AdminRole);
            await _roleManager.CreateAsync(role);
        }

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            FullName = string.IsNullOrWhiteSpace(req.FullName) ? "Administrator" : req.FullName,
            EmailConfirmed = true,
            EmailVerified = true,
            IsActive = true,
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };
        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { error = "Administrator konnte nicht erstellt werden.", details = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, DatabaseSeeder.AdminRole);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = user.Id,
            Action = AuditAction.Create,
            EntityType = "User",
            EntityId = user.Id,
            Details = "Erster Administrator über Bootstrap angelegt.",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return Ok(new { email = user.Email, message = "Administrator erstellt. Der Bootstrap ist jetzt gesperrt." });
    }

    private bool IsAllowedBootstrap()
    {
        var ip = HttpContext.Connection.RemoteIpAddress;
        var isLoopback = ip != null && IPAddress.IsLoopback(ip);
        if (isLoopback) return true;
        if (!string.IsNullOrWhiteSpace(_bootstrap.Secret))
        {
            var provided = Request.Headers["X-Bootstrap-Secret"].ToString();
            return !string.IsNullOrEmpty(provided) && provided == _bootstrap.Secret;
        }
        // Ohne konfiguriertes Geheimnis nur in der Entwicklungsumgebung.
        return _env.IsDevelopment();
    }

    public class SetupRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? FullName { get; set; }
    }
}
