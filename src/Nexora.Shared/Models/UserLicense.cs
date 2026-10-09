using System.ComponentModel.DataAnnotations;

namespace Nexora.Shared.Models;

/// <summary>
/// Verknüpfung Benutzer ↔ Lizenz (direkte Zuweisung durch Admin oder Einlösung
/// eines Lizenzschlüssels). Ein Benutzer kann mehrere Lizenzen besitzen.
/// </summary>
public class UserLicense
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required] public string UserId { get; set; } = "";

    [Required] public Guid LicenseId { get; set; }
    public License? License { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Admin, der zugewiesen hat (null bei Einlösung eines Schlüssels).</summary>
    public string? AssignedByUserId { get; set; }

    /// <summary>Quelle der Zuweisung: "admin" oder "key".</summary>
    [MaxLength(20)] public string Source { get; set; } = "admin";

    public Guid? LicenseKeyId { get; set; }

    public DateTime? RevokedAt { get; set; }
}
