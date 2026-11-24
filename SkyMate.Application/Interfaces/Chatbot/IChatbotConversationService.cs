using SkyMate.Application.DTOs.Chatbot;

namespace SkyMate.Application.Interfaces.Chatbot;

/// <summary>Defines conversation operations scoped to the authenticated owner.</summary>
public interface IChatbotConversationService
{
    /// <summary>Lists the owner's conversations without loading their messages.</summary>
    Task<IReadOnlyList<ConversationSummary>> ListForUserAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates and persists a conversation for the owner.</summary>
    Task<ConversationSummary> CreateForUserAsync(
        int userId,
        string? title,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a conversation only when it belongs to the owner.</summary>
    Task<ConversationSummary?> GetForUserAsync(
        int conversationId,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes an owned conversation and its dependent messages.</summary>
    Task<bool> DeleteForUserAsync(
        int conversationId,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists messages only when the conversation belongs to the owner.</summary>
    Task<IReadOnlyList<ConversationMessageSummary>?> ListMessagesForUserAsync(
        int conversationId,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Generates and atomically persists a user/assistant message pair.</summary>
    Task<ConversationTurnSummary?> SendMessageAsync(
        int conversationId,
        int userId,
        string content,
        int? tripId,
        CancellationToken cancellationToken = default);
}
