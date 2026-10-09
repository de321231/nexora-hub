using Microsoft.AspNetCore.Identity;

namespace Nexora.Shared.Models;

/// <summary>Benutzerkonto. Verwendet ASP.NET Identity für Passwort-Hashing.</summary>
public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }

    /// <summary>Fachliche Sperre (unabhängig von Identity-Lockout).</summary>
    public bool IsLocked { get; set; }

    /// <summary>Wird bei Deaktivierung durch einen Admin auf false gesetzt.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAtUtc { get; set; }

    public string? EmailVerifiedToken { get; set; }

    public bool EmailVerified { get; set; }
}
