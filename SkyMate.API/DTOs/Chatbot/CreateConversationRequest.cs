using System.ComponentModel.DataAnnotations;

namespace SkyMate.API.DTOs.Chatbot;

/// <summary>Optional fields accepted when creating a conversation.</summary>
public sealed class CreateConversationRequest
{
    /// <summary>An optional title of up to 200 characters.</summary>
    [StringLength(200)]
    public string? Title { get; init; }
}
