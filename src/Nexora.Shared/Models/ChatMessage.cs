using System.ComponentModel.DataAnnotations;

namespace Nexora.Shared.Models;

/// <summary>Eine einzelne Nachricht innerhalb einer Unterhaltung.</summary>
public class ChatMessage
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required] public Guid ConversationId { get; set; }
    public ChatConversation? Conversation { get; set; }

    public ChatRole Role { get; set; }

    [Required] public string Content { get; set; } = "";

    [MaxLength(200)] public string? ModelId { get; set; }

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
