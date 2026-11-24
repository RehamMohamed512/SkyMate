namespace SkyMate.API.DTOs.Chatbot;

/// <summary>Contains both persisted messages produced by a successful chat request.</summary>
public sealed record ConversationTurnResponse(
    ConversationMessageResponse UserMessage,
    ConversationMessageResponse AssistantMessage);
