using Microsoft.Extensions.Options;
using SkyMate.Application.AI;
using SkyMate.Application.Interfaces.Chatbot;
using SkyMate.Infrastructure.AI;

namespace SkyMate.Infrastructure.Services;

/// <summary>Coordinates provider-neutral prompting and text generation.</summary>
public sealed class AssistantService(
    IAssistantPromptBuilder promptBuilder,
    IAssistantLlmClient llmClient,
    IKnowledgeBaseService knowledgeBaseService,
    IOptions<AssistantAiOptions> options) : IAssistantService
{
    private readonly AssistantAiOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<AssistantLlmResponse> GenerateReplyAsync(
        string userMessage,
        IReadOnlyList<AssistantLlmTurn> history,
        AssistantTripContext? currentTripContext,
        CancellationToken cancellationToken = default)
    {
        var messages = history.TakeLast(_options.MaxHistoryMessages).ToList();
        while (messages.Count > 0 && messages[0].Role != SkyMate.Domain.Enums.ConversationRole.User)
        {
            messages.RemoveAt(0);
        }

        messages.Add(new AssistantLlmTurn(SkyMate.Domain.Enums.ConversationRole.User, userMessage));
        var knowledgeBase = await knowledgeBaseService.GetActiveAsync(cancellationToken);
        var systemInstruction = promptBuilder.BuildSystemInstruction(new AssistantPromptContext(
            knowledgeBase,
            currentTripContext,
            _options.AllowGeneralTravelQuestions));

        var response = await llmClient.GenerateAsync(
            new AssistantLlmRequest(systemInstruction, messages),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(response.Text))
        {
            throw new AssistantProviderException("The AI provider returned an empty response.");
        }

        return response with { Text = response.Text.Trim() };
    }
}
