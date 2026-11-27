using System.ComponentModel.DataAnnotations;

namespace SkyMate.API.DTOs.Chatbot;

/// <summary>Fields accepted when creating a trip-associated conversation.</summary>
public sealed class CreateConversationRequest
{
    /// <summary>The trip associated with the conversation.</summary>
    [Range(1, int.MaxValue)]
    public int TripId { get; init; }

    /// <summary>An optional title of up to 200 characters.</summary>
    [StringLength(200)]
    public string? Title { get; init; }
}
