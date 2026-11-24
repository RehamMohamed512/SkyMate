using SkyMate.Application.AI;

namespace SkyMate.Application.Interfaces.Chatbot;

/// <summary>Communicates with one replaceable text-generation provider.</summary>
public interface IAssistantLlmClient
{
    /// <summary>Generates one assistant reply from provider-neutral input.</summary>
    Task<AssistantLlmResponse> GenerateAsync(
        AssistantLlmRequest request,
        CancellationToken cancellationToken = default);
}
