namespace SkyMate.Domain.Entities;

/// <summary>Represents a chatbot conversation owned by a user.</summary>
public sealed class AiConversation
{
    public int Id { get; set; }
    /// <summary>The conversation owner. The API takes this value from the authenticated JWT.</summary>
    public int UserId { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Messages in this conversation. Deleting the conversation also deletes them.</summary>
    public ICollection<AiConversationMessage> Messages { get; set; } = new List<AiConversationMessage>();
}
