using System.ComponentModel.DataAnnotations;

namespace SkyMate.API.DTOs.Chatbot;

/// <summary>Contains the message text. The server sets its role and owner.</summary>
public sealed class AddConversationMessageRequest
{
    /// <summary>The required text message, up to 1,000 characters.</summary>
    public string? Content { get; init; }

    /// <summary>Optional context for this message only; it is never saved with the conversation.</summary>
    [Range(1, int.MaxValue)]
    public int? TripId { get; init; }
}
