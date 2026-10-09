using System.ComponentModel.DataAnnotations;

namespace Nexora.Shared.Models;

/// <summary>
/// Ein einlösbarer Lizenzschlüssel. Wird niemals im Klartext gespeichert,
/// sondern nur als SHA-256-Hash. Der Klartext wird ausschließlich bei der
/// Erstellung einmalig zurückgegeben.
/// </summary>
public class LicenseKey
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required] public Guid LicenseId { get; set; }
    public License? License { get; set; }

    /// <summary>SHA-256-Hash des normalisierten Klartextschlüssels.</summary>
    [Required, MaxLength(128)] public string KeyHash { get; set; } = "";

    /// <summary>Anzeigepräfix, z. B. "NXAI-A1B2".</summary>
    [MaxLength(32)] public string KeyPrefix { get; set; } = "";

    public LicenseKeyStatus Status { get; set; } = LicenseKeyStatus.Available;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    /// <summary>Maximale Einlösungen (in der Regel 1).</summary>
    public int MaxUses { get; set; } = 1;

    public int UsedCount { get; set; }

    public string? RedeemedByUserId { get; set; }

    public DateTime? RedeemedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    [MaxLength(200)] public string? Note { get; set; }
}
