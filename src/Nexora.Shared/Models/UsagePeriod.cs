using System.ComponentModel.DataAnnotations;

namespace Nexora.Shared.Models;

/// <summary>
/// Aggregierte Verbrauchszähler pro Benutzer + Lizenz + Zeitraum (Tag/Monat).
/// Grundlage für die serverseitige Durchsetzung der Kontingente.
/// </summary>
public class UsagePeriod
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required] public string UserId { get; set; } = "";

    [Required] public Guid LicenseId { get; set; }

    public UsagePeriodType PeriodType { get; set; }

    /// <summary>Beginn des Zeitraums in UTC (00:00:00Z für Tag, Monatsanfang für Monat).</summary>
    public DateTime PeriodStartUtc { get; set; }

    public int MessageCount { get; set; }

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long TotalTokens { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
