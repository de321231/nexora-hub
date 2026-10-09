using System.ComponentModel.DataAnnotations;
namespace Nexora.Shared.Models;
public class AppVersion
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public Guid AppId { get; set; }
    public AppDefinition? App { get; set; }
    [Required, MaxLength(20)] public string VersionNumber { get; set; } = "1.0.0";
    [MaxLength(500)] public string ReleaseNotes { get; set; } = "";
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public bool IsLatest { get; set; } = true;
}
