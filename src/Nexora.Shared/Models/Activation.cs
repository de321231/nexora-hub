using System.ComponentModel.DataAnnotations;
namespace Nexora.Shared.Models;
public class Activation
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public Guid LicenseId { get; set; }
    public License? License { get; set; }
    [Required] public Guid DeviceId { get; set; }
    public Device? Device { get; set; }
    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastValidatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
