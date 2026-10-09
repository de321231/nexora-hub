using System.ComponentModel.DataAnnotations;
namespace Nexora.Shared.Models;
public class AuditLog
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? ActorUserId { get; set; }
    [Required, MaxLength(20)] public AuditAction Action { get; set; }
    [Required, MaxLength(50)] public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    [MaxLength(2000)] public string Details { get; set; } = "";
    [MaxLength(45)] public string? IpAddress { get; set; }
}
