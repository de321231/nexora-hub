using System.ComponentModel.DataAnnotations;

namespace Nexora.Shared.Models;

/// <summary>
/// Eine Lizenz (Nutzungsrecht) für eine App (<see cref="Product"/>).
/// Enthält alle serverseitig erzwungenen Kontingente.
/// Nullable-Limits bedeuten "unbegrenzt".
/// </summary>
public class License
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Optionale Bezeichnung, z. B. "Team Jahrestarif".</summary>
    [MaxLength(200)] public string? Name { get; set; }

    /// <summary>Bevorzugter Lizenzschlüssel-Präfix (z. B. "NXAI").</summary>
    [MaxLength(20)] public string KeyPrefix { get; set; } = "NX";

    [Required] public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public LicenseStatus Status { get; set; } = LicenseStatus.Pending;

    // --- Zeitliche Gültigkeit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    [MaxLength(2000)] public string? AdminNotes { get; set; }

    // --- Geräte ---
    public int MaxDevices { get; set; } = 1;

    // --- Nachrichtenlimits (null = unbegrenzt) ---
    public int? MaxMessagesPerDay { get; set; }
    public int? MaxMessagesPerMonth { get; set; }

    // --- Tokenlimits Tag ---
    public long? MaxInputTokensPerDay { get; set; }
    public long? MaxOutputTokensPerDay { get; set; }
    public long? MaxTotalTokensPerDay { get; set; }

    // --- Tokenlimits Monat ---
    public long? MaxInputTokensPerMonth { get; set; }
    public long? MaxOutputTokensPerMonth { get; set; }
    public long? MaxTotalTokensPerMonth { get; set; }

    /// <summary>Optionale Obergrenze pro einzelner Anfrage.</summary>
    public long? MaxTokensPerRequest { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
