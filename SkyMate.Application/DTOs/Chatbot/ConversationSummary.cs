namespace SkyMate.Application.DTOs.Chatbot;

/// <summary>Contains conversation details without loading its messages.</summary>
public sealed record ConversationSummary(
    int Id,
    int? TripId,
    string? Title,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
