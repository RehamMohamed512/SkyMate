using SkyMate.Domain.Enums;

namespace SkyMate.Application.DTOs.Chatbot;

/// <summary>Contains a conversation message returned to the API layer.</summary>
public sealed record ConversationMessageSummary(
    int Id,
    ConversationRole Role,
    string Content,
    DateTime CreatedAt);
