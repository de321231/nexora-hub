using System.ComponentModel.DataAnnotations;

namespace Nexora.Shared.Models;

/// <summary>
/// Ein über LiteLLM verfügbares KI-Modell. Modelle werden zentral verwaltet
/// und einzelnen Lizenzen zugeordnet (<see cref="LicenseModel"/>).
/// </summary>
public class AiModel
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Modellkennung, wie sie an LiteLLM übergeben wird.</summary>
    [Required, MaxLength(200)] public string ModelId { get; set; } = "";

    [MaxLength(200)] public string DisplayName { get; set; } = "";

    [MaxLength(100)] public string Provider { get; set; } = "";

    public bool IsActive { get; set; } = true;

    /// <summary>Optionales Kontextlimit (nur informativ).</summary>
    public int? MaxContextTokens { get; set; }

    /// <summary>Kosten pro 1M Eingabe-Tokens in USD, falls von LiteLLM bekannt.</summary>
    public decimal? InputCostPer1M { get; set; }

    /// <summary>Kosten pro 1M Ausgabe-Tokens in USD, falls bekannt.</summary>
    public decimal? OutputCostPer1M { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
