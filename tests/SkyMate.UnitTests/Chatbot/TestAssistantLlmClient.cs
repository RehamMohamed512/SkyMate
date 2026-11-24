using SkyMate.Application.AI;
using SkyMate.Application.Interfaces.Chatbot;

namespace SkyMate.UnitTests.Chatbot;

/// <summary>Deterministic provider fake for HTTP and conversation persistence tests.</summary>
internal sealed class TestAssistantLlmClient : IAssistantLlmClient
{
    public bool FailRequests { get; set; }
    public string? ProviderContextJsonToReturn { get; set; }
    public List<AssistantLlmRequest> Requests { get; } = [];

    public Task<AssistantLlmResponse> GenerateAsync(
        AssistantLlmRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        if (FailRequests)
        {
            throw new AssistantProviderException("Test provider failure.");
        }

        var currentMessage = request.Messages.LastOrDefault()?.Content ?? string.Empty;
        return Task.FromResult(new AssistantLlmResponse(
            $"Travel reply: {currentMessage}",
            ProviderContextJsonToReturn));
    }
}
