using System.ComponentModel.DataAnnotations;
namespace Nexora.Shared.Models;
public class Download
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public Guid AppVersionId { get; set; }
    public AppVersion? AppVersion { get; set; }
    [Required, MaxLength(1000)] public string Url { get; set; } = "";
    public long FileSizeBytes { get; set; }
    [Required, MaxLength(64)] public string Sha256Hash { get; set; } = "";
    [MaxLength(200)] public string MinOsVersion { get; set; } = "Windows 10";
}
