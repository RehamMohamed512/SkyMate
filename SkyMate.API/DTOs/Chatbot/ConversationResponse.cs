namespace SkyMate.API.DTOs.Chatbot;

/// <summary>Conversation details returned by read and create endpoints.</summary>
public sealed record ConversationResponse(
    int Id,
    int? TripId,
    string? Title,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
