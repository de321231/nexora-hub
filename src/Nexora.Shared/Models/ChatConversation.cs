using System.ComponentModel.DataAnnotations;

namespace Nexora.Shared.Models;

/// <summary>Eine Chat-Unterhaltung eines Benutzers.</summary>
public class ChatConversation
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required] public string UserId { get; set; } = "";

    public Guid? LicenseId { get; set; }

    [MaxLength(300)] public string Title { get; set; } = "Neue Unterhaltung";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
