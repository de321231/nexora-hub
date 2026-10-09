using System.ComponentModel.DataAnnotations;
namespace Nexora.Shared.Models;
public class AppDefinition
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(120)] public string DisplayName { get; set; } = "";
    [MaxLength(1000)] public string Description { get; set; } = "";
    [MaxLength(200)] public string IconUrl { get; set; } = "";
    [Required] public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public AppType Type { get; set; } = AppType.WebApp;
    public AppStatus Status { get; set; } = AppStatus.Draft;
    public bool LicenseRequired { get; set; } = false;
    [MaxLength(500)] public string? WebUrl { get; set; }
    [MaxLength(500)] public string? AllowedOrigins { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
