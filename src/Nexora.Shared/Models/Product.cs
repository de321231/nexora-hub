using System.ComponentModel.DataAnnotations;

namespace Nexora.Shared.Models;

/// <summary>
/// Eine registrierte, lizenzierbare Anwendung (z. B. "Nexora AI").
/// Wird im Adminpanel als "App" bezeichnet und ist die Einheit, an die
/// Lizenzen gebunden sind.
/// </summary>
public class Product
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(120)] public string Name { get; set; } = "";

    /// <summary>Stabile technische Kennung, z. B. "nexora-ai".</summary>
    [Required, MaxLength(80)] public string Slug { get; set; } = "";

    [MaxLength(500)] public string Description { get; set; } = "";

    [MaxLength(200)] public string IconUrl { get; set; } = "";

    public AppType Kind { get; set; } = AppType.WindowsProgram;

    public AppStatus Status { get; set; } = AppStatus.Draft;

    public bool LicenseRequired { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
