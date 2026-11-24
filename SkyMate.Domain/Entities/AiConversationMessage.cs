using SkyMate.Domain.Enums;

namespace SkyMate.Domain.Entities;

/// <summary>Represents a user or assistant message in a chatbot conversation.</summary>
public sealed class AiConversationMessage
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public ConversationRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    /// <summary>Opaque provider continuation data, excluded from user-facing message responses.</summary>
    public string? ProviderContextJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AiConversation Conversation { get; set; } = null!;
}
