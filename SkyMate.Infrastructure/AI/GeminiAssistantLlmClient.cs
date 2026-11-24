using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkyMate.Application.AI;
using SkyMate.Application.Interfaces.Chatbot;
using SkyMate.Domain.Enums;

namespace SkyMate.Infrastructure.AI;

/// <summary>Calls Gemini's stateless Interactions API and switches models on transient failures.</summary>
public sealed class GeminiAssistantLlmClient(
    HttpClient httpClient,
    IOptions<AssistantAiOptions> options,
    ILogger<GeminiAssistantLlmClient>? logger = null) : IAssistantLlmClient
{
    private const int ModelSwitchDelayMilliseconds = 300;
    private const string LegacyProviderContextModel = "gemini-3.8-flash";
    private readonly AssistantAiOptions _options = options.Value;
    private readonly ILogger<GeminiAssistantLlmClient>? _logger = logger;

    /// <inheritdoc />
    public async Task<AssistantLlmResponse> GenerateAsync(
        AssistantLlmRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new AssistantConfigurationException(
                "The Gemini API key is missing. Configure AssistantAi:ApiKey with User Secrets for development or AssistantAi__ApiKey in production.");
        }

        var models = new[] { _options.Model }
            .Concat(_options.FallbackModels ?? [])
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var sawRateLimit = false;

        for (var index = 0; index < models.Length; index++)
        {
            var model = models[index];
            var isLastModel = index == models.Length - 1;
            var modelRequest = new GeminiInteractionRequest(
                model,
                request.SystemInstruction,
                request.Messages.SelectMany(turn => ToInputSteps(turn, model)).ToArray(),
                Store: false,
                new GeminiGenerationConfig(
                    MaxOutputTokens: 2048,
                    Temperature: SupportsTemperature(model) ? 0.3 : null));

            using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);

            try
            {
                using var response = await SendAsync(modelRequest, linkedSource.Token);
                if (!response.IsSuccessStatusCode)
                {
                    var httpStatus = (int)response.StatusCode;
                    sawRateLimit |= response.StatusCode == HttpStatusCode.TooManyRequests;

                    var isTransient = httpStatus is 429 or 500 or 502 or 503 or 504;
                    if (isTransient && !isLastModel)
                    {
                        _logger?.LogWarning(
                            "Gemini returned HTTP {StatusCode} for model {Model}; trying fallback model {FallbackModel}.",
                            httpStatus,
                            model,
                            models[index + 1]);
                        await Task.Delay(ModelSwitchDelayMilliseconds, cancellationToken);
                        continue;
                    }

                    if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or
                        HttpStatusCode.NotFound)
                    {
                        throw new AssistantConfigurationException(
                            $"Gemini rejected model '{model}' or the configured API key. Verify AssistantAi:ApiKey and AssistantAi:Model.");
                    }

                    if (response.StatusCode == HttpStatusCode.TooManyRequests || sawRateLimit)
                    {
                        throw new AssistantProviderRateLimitException(
                            "The Gemini quota or rate limit was reached for the configured models.");
                    }

                    string? providerDetail;
                    try
                    {
                        providerDetail = await ReadProviderErrorMessageAsync(response, linkedSource.Token);
                    }
                    catch (HttpRequestException)
                    {
                        providerDetail = null;
                    }

                    var detailSuffix = string.IsNullOrWhiteSpace(providerDetail)
                        ? string.Empty
                        : $" Provider message: {providerDetail}";
                    _logger?.LogError(
                        "Gemini failed for model {Model} with HTTP {StatusCode}. Provider message: {ProviderMessage}",
                        model,
                        httpStatus,
                        providerDetail);
                    throw new AssistantProviderException(
                        $"The AI provider returned HTTP {httpStatus} ({response.StatusCode}) for model '{model}'.{detailSuffix}");
                }

                return await ReadSuccessfulResponseAsync(response, model, linkedSource.Token);
            }
            catch (OperationCanceledException exception) when (timeoutSource.IsCancellationRequested &&
                                                               !cancellationToken.IsCancellationRequested)
            {
                if (!isLastModel)
                {
                    _logger?.LogWarning(
                        exception,
                        "Gemini timed out for model {Model}; trying fallback model {FallbackModel}.",
                        model,
                        models[index + 1]);
                    await Task.Delay(ModelSwitchDelayMilliseconds, cancellationToken);
                    continue;
                }

                throw new AssistantProviderTimeoutException(
                    $"The AI provider did not respond within {_options.TimeoutSeconds} seconds for model '{model}'.",
                    exception);
            }
            catch (HttpRequestException exception)
            {
                if (!isLastModel)
                {
                    _logger?.LogWarning(
                        exception,
                        "Gemini network error for model {Model}; trying fallback model {FallbackModel}.",
                        model,
                        models[index + 1]);
                    await Task.Delay(ModelSwitchDelayMilliseconds, cancellationToken);
                    continue;
                }

                throw new AssistantProviderException(
                    $"The AI provider could not be reached for model '{model}'.",
                    exception);
            }
        }

        throw new AssistantProviderException("All configured Gemini models failed without a usable response.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        GeminiInteractionRequest interactionRequest,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1beta/interactions");
        message.Headers.Add("x-goog-api-key", _options.ApiKey);
        message.Content = JsonContent.Create(interactionRequest);
        return await httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    private async Task<AssistantLlmResponse> ReadSuccessfulResponseAsync(
        HttpResponseMessage response,
        string requestedModel,
        CancellationToken cancellationToken)
    {
        GeminiInteractionResponse? providerResponse;
        string responseBody;
        try
        {
            responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            providerResponse = JsonSerializer.Deserialize<GeminiInteractionResponse>(responseBody);
        }
        catch (JsonException exception)
        {
            throw new AssistantProviderException("The AI provider returned an invalid response.", exception);
        }

        if (providerResponse?.Status is { Length: > 0 } status &&
            !string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new AssistantProviderException($"The AI provider returned an interaction with status '{status}'.");
        }

        var output = providerResponse?.OutputText;
        if (string.IsNullOrWhiteSpace(output))
        {
            output = string.Join(
                string.Empty,
                providerResponse?.Steps?
                    .Where(step => string.Equals(step.Type, "model_output", StringComparison.Ordinal))
                    .SelectMany(step => step.Content ?? [])
                    .Where(part => string.Equals(part.Type, "text", StringComparison.Ordinal))
                    .Select(part => part.Text) ?? []);
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new AssistantProviderException("The AI provider response contained no text output.");
        }

        return new AssistantLlmResponse(
            output.Trim(),
            ExtractProviderContextJson(responseBody, providerResponse?.Model ?? requestedModel));
    }

    private static async Task<string?> ReadProviderErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                var value = message.GetString()?.Trim();
                return string.IsNullOrEmpty(value) ? null : value[..Math.Min(value.Length, 500)];
            }
        }
        catch (JsonException)
        {
            // Some upstream failures have a non-JSON body; keep the status code either way.
        }

        return null;
    }

    private static IReadOnlyList<JsonElement> ToInputSteps(AssistantLlmTurn turn, string model)
    {
        if (turn.Role == ConversationRole.Assistant && !string.IsNullOrWhiteSpace(turn.ProviderContextJson))
        {
            try
            {
                using var document = JsonDocument.Parse(turn.ProviderContextJson);
                var root = document.RootElement;
                JsonElement steps;
                var contextMatchesModel = false;

                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("model", out var storedModel) &&
                    storedModel.ValueKind == JsonValueKind.String &&
                    root.TryGetProperty("steps", out steps))
                {
                    contextMatchesModel = string.Equals(
                        storedModel.GetString(),
                        model,
                        StringComparison.OrdinalIgnoreCase);
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    // Older rows stored only steps and were generated by the original 3.8 default.
                    steps = root;
                    contextMatchesModel = string.Equals(
                        model,
                        LegacyProviderContextModel,
                        StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    steps = default;
                }

                if (contextMatchesModel && steps.ValueKind == JsonValueKind.Array)
                {
                    var storedSteps = steps.EnumerateArray()
                        .Where(step => step.ValueKind == JsonValueKind.Object)
                        .Select(step => step.Clone())
                        .ToArray();
                    if (storedSteps.Length > 0)
                    {
                        return storedSteps;
                    }
                }
            }
            catch (JsonException)
            {
                // Old or invalid metadata falls back to the user-visible assistant text.
            }
        }

        var type = turn.Role == ConversationRole.Assistant ? "model_output" : "user_input";
        return
        [
            JsonSerializer.SerializeToElement(new GeminiInputStep(
                type,
                [new GeminiTextPart("text", turn.Content)]))
        ];
    }

    private static string? ExtractProviderContextJson(string responseBody, string model)
    {
        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("steps", out var steps) ||
            steps.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var reusableSteps = steps.EnumerateArray()
            .Where(step => step.ValueKind == JsonValueKind.Object &&
                           step.TryGetProperty("type", out var type) &&
                           type.ValueKind == JsonValueKind.String &&
                           type.GetString() is "thought" or "model_output")
            .Select(step => step.Clone())
            .ToArray();

        return reusableSteps.Length == 0
            ? null
            : JsonSerializer.Serialize(new GeminiProviderContext(model, reusableSteps));
    }

    // Gemini 3.6+ ignores sampling parameters and reports a deprecation warning. Keep the AI team's
    // temperature for supported model families, while omitting it for the current 3.8 default.
    private static bool SupportsTemperature(string model) =>
        model.StartsWith("gemini-3.5-", StringComparison.OrdinalIgnoreCase) ||
        model.StartsWith("gemini-3.1-", StringComparison.OrdinalIgnoreCase) ||
        model.StartsWith("gemini-2.", StringComparison.OrdinalIgnoreCase);

    private sealed record GeminiInteractionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("system_instruction")] string SystemInstruction,
        [property: JsonPropertyName("input")] IReadOnlyList<JsonElement> Input,
        [property: JsonPropertyName("store")] bool Store,
        [property: JsonPropertyName("generation_config")] GeminiGenerationConfig GenerationConfig);

    private sealed record GeminiGenerationConfig(
        [property: JsonPropertyName("max_output_tokens")] int MaxOutputTokens,
        [property: JsonPropertyName("temperature")]
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        double? Temperature);

    private sealed record GeminiInputStep(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("content")] IReadOnlyList<GeminiTextPart> Content);

    private sealed record GeminiTextPart(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text);

    private sealed record GeminiInteractionResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("output_text")] string? OutputText,
        [property: JsonPropertyName("steps")] IReadOnlyList<GeminiOutputStep>? Steps);

    private sealed record GeminiProviderContext(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("steps")] IReadOnlyList<JsonElement> Steps);

    private sealed record GeminiOutputStep(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("content")] IReadOnlyList<GeminiTextPart>? Content);
}
