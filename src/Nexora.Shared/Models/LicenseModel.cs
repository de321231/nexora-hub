using System.ComponentModel.DataAnnotations;

namespace Nexora.Shared.Models;

/// <summary>Zuordnung erlaubtes Modell ↔ Lizenz.</summary>
public class LicenseModel
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required] public Guid LicenseId { get; set; }
    public License? License { get; set; }

    [Required] public Guid AiModelId { get; set; }
    public AiModel? AiModel { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
