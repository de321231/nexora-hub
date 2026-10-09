using System.ComponentModel.DataAnnotations;

namespace Nexora.Shared.Models;

/// <summary>
/// Einzelne protokollierte KI-Anfrage. Bewusst ohne Nachrichteninhalt.
/// </summary>
public class UsageEvent
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    public string? UserId { get; set; }

    public Guid? LicenseId { get; set; }

    public Guid? ProductId { get; set; }

    public Guid? AiModelId { get; set; }

    [MaxLength(200)] public string? ModelId { get; set; }

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int TotalTokens { get; set; }

    /// <summary>Geschätzt, wenn der Provider keine Nutzungsdaten liefert.</summary>
    public bool TokensEstimated { get; set; }

    public bool Success { get; set; }

    [MaxLength(200)] public string? ErrorCode { get; set; }

    public int? LatencyMs { get; set; }

    /// <summary>Kosten in USD, nur wenn von LiteLLM geliefert. Sonst null.</summary>
    public decimal? CostUsd { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
