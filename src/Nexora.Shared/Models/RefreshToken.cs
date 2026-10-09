using System.ComponentModel.DataAnnotations;

namespace Nexora.Shared.Models;

/// <summary>
/// Refresh-Token für native Clients. Es wird nur der Hash gespeichert,
/// damit ein Datenbankleck keine gültigen Tokens offenlegt.
/// </summary>
public class RefreshToken
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required] public string UserId { get; set; } = "";

    [Required, MaxLength(128)] public string TokenHash { get; set; } = "";

    [MaxLength(128)] public string DeviceIdentifier { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? ReplacedByTokenHash { get; set; }
}
