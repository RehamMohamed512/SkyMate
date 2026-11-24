using SkyMate.Application.AI;

namespace SkyMate.Application.Interfaces.Chatbot;

/// <summary>Coordinates prompt construction and text generation without managing persistence.</summary>
public interface IAssistantService
{
    /// <summary>Generates a reply using the supplied conversation history.</summary>
    Task<AssistantLlmResponse> GenerateReplyAsync(
        string userMessage,
        IReadOnlyList<AssistantLlmTurn> history,
        AssistantTripContext? currentTripContext,
        CancellationToken cancellationToken = default);
}
