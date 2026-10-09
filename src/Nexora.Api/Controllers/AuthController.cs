using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexora.Api.Data;
using Nexora.Api.Options;
using Nexora.Api.Services;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokens;
    private readonly IAuditService _audit;
    private readonly JwtOptions _jwt;

    public AuthController(AppDbContext db, UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager, ITokenService tokens,
        IAuditService audit, IOptions<JwtOptions> jwt)
    {
        _db = db;
        _userManager = userManager;
        _signInManager = signInManager;
        _tokens = tokens;
        _audit = audit;
        _jwt = jwt.Value;
    }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost("register")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var existing = await _userManager.FindByEmailAsync(req.Email);
        if (existing != null)
            return Conflict(new { error = "Diese E-Mail ist bereits registriert." });

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            FullName = req.FullName,
            EmailConfirmed = true,
            EmailVerified = true,
            IsActive = true,
            IsLocked = false,
            CreatedAt = DateTime.UtcNow
        };
        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { error = "Registrierung fehlgeschlagen.", details = result.Errors.Select(e => e.Description) });

        // Registrierung vergibt niemals Adminrechte.
        await _userManager.AddToRoleAsync(user, DatabaseSeeder.UserRole);

        _audit.Add(user.Id, AuditAction.Register, "User", user.Id, $"Registrierung {user.Email}", Ip);
        await _db.SaveChangesAsync();

        var auth = await IssueTokensAsync(user);
        return Ok(auth);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null)
        {
            _audit.Add(null, AuditAction.LoginFailed, "User", null, $"Unbekannte E-Mail: {req.Email}", Ip);
            await _db.SaveChangesAsync();
            return Unauthorized(new { error = "E-Mail oder Passwort ist falsch." });
        }

        if (!user.IsActive || user.IsLocked)
        {
            _audit.Add(user.Id, AuditAction.LoginFailed, "User", user.Id, "Konto deaktiviert/gesperrt", Ip);
            await _db.SaveChangesAsync();
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Dieses Konto ist deaktiviert." });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            _audit.Add(user.Id, AuditAction.LoginFailed, "User", user.Id, "Konto temporär gesperrt (Brute-Force-Schutz)", Ip);
            await _db.SaveChangesAsync();
            return StatusCode(StatusCodes.Status423Locked, new { error = "Konto wegen zu vieler Fehlversuche vorübergehend gesperrt." });
        }
        if (!result.Succeeded)
        {
            _audit.Add(user.Id, AuditAction.LoginFailed, "User", user.Id, "Falsches Passwort", Ip);
            await _db.SaveChangesAsync();
            return Unauthorized(new { error = "E-Mail oder Passwort ist falsch." });
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        _audit.Add(user.Id, AuditAction.Login, "User", user.Id, "Anmeldung", Ip);
        await _db.SaveChangesAsync();

        var auth = await IssueTokensAsync(user, req.DeviceIdentifier);
        return Ok(auth);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var hash = _tokens.HashRefreshToken(req.RefreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (token == null || token.RevokedAt != null || token.ExpiresAt <= DateTime.UtcNow)
            return Unauthorized(new { error = "Sitzung abgelaufen. Bitte erneut anmelden." });

        var user = await _userManager.FindByIdAsync(token.UserId);
        if (user == null || !user.IsActive || user.IsLocked)
            return Unauthorized(new { error = "Konto nicht verfügbar." });

        // Rotation: altes Token widerrufen.
        token.RevokedAt = DateTime.UtcNow;
        var auth = await IssueTokensAsync(user, token.DeviceIdentifier);
        return Ok(auth);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest? req)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var tokens = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync();
        if (req != null && !string.IsNullOrWhiteSpace(req.RefreshToken))
        {
            var hash = _tokens.HashRefreshToken(req.RefreshToken);
            tokens = tokens.Where(t => t.TokenHash == hash).ToList();
        }
        foreach (var t in tokens) t.RevokedAt = DateTime.UtcNow;
        _audit.Add(userId, AuditAction.Logout, "User", userId, "Abmeldung", Ip);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Abgemeldet." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.FindByIdAsync(User.GetUserId() ?? "");
        if (user == null) return Unauthorized();
        var roles = await _userManager.GetRolesAsync(user);
        var active = await _db.UserLicenses.CountAsync(ul => ul.UserId == user.Id && ul.RevokedAt == null);
        return Ok(DtoMapper.ToDto(user, roles, active));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var user = await _userManager.FindByIdAsync(User.GetUserId() ?? "");
        if (user == null) return Unauthorized();
        var result = await _userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { error = "Passwort konnte nicht geändert werden.", details = result.Errors.Select(e => e.Description) });
        // Alle Sitzungen beenden.
        var tokens = await _db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync();
        foreach (var t in tokens) t.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Passwort geändert. Bitte erneut anmelden." });
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, string? deviceIdentifier = null)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, expires) = _tokens.CreateAccessToken(user, roles);

        var raw = _tokens.CreateRefreshTokenRaw();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokens.HashRefreshToken(raw),
            DeviceIdentifier = deviceIdentifier ?? "",
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays)
        });
        await _db.SaveChangesAsync();

        var active = await _db.UserLicenses.CountAsync(ul => ul.UserId == user.Id && ul.RevokedAt == null);
        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = raw,
            AccessTokenExpiresAtUtc = expires,
            User = DtoMapper.ToDto(user, roles, active)
        };
    }
}
