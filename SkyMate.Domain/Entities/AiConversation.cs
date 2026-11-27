namespace SkyMate.Domain.Entities;

/// <summary>Represents a chatbot conversation owned by a user and associated with a trip.</summary>
public sealed class AiConversation
{
    public int Id { get; set; }
    /// <summary>The conversation owner. The API takes this value from the authenticated JWT.</summary>
    public int UserId { get; set; }
    /// <summary>The associated trip. Null is retained for conversations created before trip linking.</summary>
    public int? TripId { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Messages in this conversation. Deleting the conversation also deletes them.</summary>
    public ICollection<AiConversationMessage> Messages { get; set; } = new List<AiConversationMessage>();

    /// <summary>The associated trip, when this conversation was created after trip linking.</summary>
    public Trip? Trip { get; set; }
}
