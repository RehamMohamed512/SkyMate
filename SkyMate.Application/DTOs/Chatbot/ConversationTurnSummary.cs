namespace SkyMate.Application.DTOs.Chatbot;

/// <summary>Contains both persisted messages created by one successful chat request.</summary>
public sealed record ConversationTurnSummary(
    ConversationMessageSummary UserMessage,
    ConversationMessageSummary AssistantMessage);
