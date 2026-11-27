namespace SkyMate.API.DTOs.Chatbot;

/// <summary>Contains the message text. The server sets its role and owner.</summary>
public sealed class AddConversationMessageRequest
{
    /// <summary>The required text message, up to 1,000 characters.</summary>
    public string? Content { get; init; }

}
