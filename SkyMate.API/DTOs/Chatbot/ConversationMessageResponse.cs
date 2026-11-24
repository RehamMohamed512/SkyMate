using SkyMate.Domain.Enums;

namespace SkyMate.API.DTOs.Chatbot;

/// <summary>A message returned by a conversation endpoint.</summary>
public sealed record ConversationMessageResponse(
    int Id,
    ConversationRole Role,
    string Content,
    DateTime CreatedAtUtc);
