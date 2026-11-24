using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SkyMate.Application.AI;
using SkyMate.Infrastructure.AI;
using SkyMate.Domain.Enums;

namespace SkyMate.UnitTests.Chatbot;

/// <summary>Verifies provider configuration, wire contract, cancellation, and timeout handling.</summary>
public sealed class AssistantProviderTests
{
    [Fact]
    public void BuildsTheOriginalSystemPromptWithoutRequiringAnExternalPromptSetting()
    {
        var builder = new AssistantPromptBuilder();

        var prompt = builder.BuildSystemInstruction(new AssistantPromptContext([], null, false));

        Assert.Contains("same language the user writes in (Arabic or English)", prompt, StringComparison.Ordinal);
        Assert.Contains("Never create or modify trips", prompt, StringComparison.Ordinal);
        Assert.Contains("Never invent, estimate, calculate, or recalculate money figures", prompt, StringComparison.Ordinal);
        Assert.Contains("ACTIVE KNOWLEDGE BASE JSON", prompt, StringComparison.Ordinal);
        Assert.Contains("CURRENT TRIP CONTEXT JSON", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingApiKeyRaisesConfigurationExceptionWithoutSendingRequest()
    {
        var handler = new DelegateHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var httpClient = CreateHttpClient(handler);
        var provider = new GeminiAssistantLlmClient(
            httpClient,
            Options.Create(new AssistantAiOptions { ApiKey = string.Empty }));

        await Assert.ThrowsAsync<AssistantConfigurationException>(() =>
            provider.GenerateAsync(Request("Hello")));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SendsStatelessHistoryToGeminiAndParsesModelOutput()
    {
        const string responseJson = """
            {"status":"completed","steps":[{"type":"model_output","content":[{"type":"text","text":"Travel answer"}]}]}
            """;
        string? observedRequestJson = null;
        string? observedApiKey = null;
        var handler = new DelegateHandler(async (request, cancellationToken) =>
        {
            observedRequestJson = await request.Content!.ReadAsStringAsync(cancellationToken);
            observedApiKey = request.Headers.GetValues("x-goog-api-key").Single();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = new GeminiAssistantLlmClient(httpClient, Options.Create(new AssistantAiOptions
        {
            ApiKey = "unit-test-key",
            Model = "gemini-3.8-flash"
        }));

        var response = await provider.GenerateAsync(Request("Hello"));

        Assert.Equal("Travel answer", response.Text);
        Assert.Equal("unit-test-key", observedApiKey);
        Assert.NotNull(observedRequestJson);
        using var body = JsonDocument.Parse(observedRequestJson);
        var root = body.RootElement;
        Assert.Equal("gemini-3.8-flash", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("store").GetBoolean());
        Assert.Equal("You are SkyMate.", root.GetProperty("system_instruction").GetString());
        Assert.Equal(2048, root.GetProperty("generation_config").GetProperty("max_output_tokens").GetInt32());
        Assert.False(root.GetProperty("generation_config").TryGetProperty("temperature", out _));
        Assert.Equal("user_input", root.GetProperty("input")[0].GetProperty("type").GetString());
        Assert.Equal("Hello", root.GetProperty("input")[0].GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendsTemperatureForModelFamiliesThatStillSupportIt()
    {
        string? observedRequestJson = null;
        var handler = new DelegateHandler(async (request, cancellationToken) =>
        {
            observedRequestJson = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"output_text\":\"ok\"}",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = new GeminiAssistantLlmClient(httpClient, Options.Create(new AssistantAiOptions
        {
            ApiKey = "unit-test-key",
            Model = "gemini-3.5-flash-lite"
        }));

        await provider.GenerateAsync(Request("Hello"));

        using var body = JsonDocument.Parse(observedRequestJson!);
        Assert.Equal(0.3, body.RootElement.GetProperty("generation_config").GetProperty("temperature").GetDouble());
    }

    [Fact]
    public async Task StatelessHistoryReplaysGeminiThoughtSignaturesWithoutExposingThemAsMessageText()
    {
        const string responseJson = """
            {"status":"completed","steps":[{"type":"thought","signature":"opaque-signature"},{"type":"model_output","content":[{"type":"text","text":"Travel answer"}]}]}
            """;
        var observedRequests = new List<string>();
        var handler = new DelegateHandler(async (request, cancellationToken) =>
        {
            observedRequests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = new GeminiAssistantLlmClient(httpClient, Options.Create(new AssistantAiOptions
        {
            ApiKey = "unit-test-key",
            Model = "gemini-3.8-flash"
        }));

        var firstResponse = await provider.GenerateAsync(Request("First question"));
        Assert.Contains("opaque-signature", firstResponse.ProviderContextJson, StringComparison.Ordinal);
        var secondRequest = new AssistantLlmRequest(
            "You are SkyMate.",
            [
                new AssistantLlmTurn(ConversationRole.User, "First question"),
                new AssistantLlmTurn(ConversationRole.Assistant, firstResponse.Text, firstResponse.ProviderContextJson),
                new AssistantLlmTurn(ConversationRole.User, "Follow-up")
            ]);
        await provider.GenerateAsync(secondRequest);

        using var secondBody = JsonDocument.Parse(observedRequests[1]);
        var input = secondBody.RootElement.GetProperty("input");
        Assert.Equal(4, input.GetArrayLength());
        Assert.Equal("user_input", input[0].GetProperty("type").GetString());
        Assert.Equal("thought", input[1].GetProperty("type").GetString());
        Assert.Equal("opaque-signature", input[1].GetProperty("signature").GetString());
        Assert.Equal("model_output", input[2].GetProperty("type").GetString());
        Assert.Equal("user_input", input[3].GetProperty("type").GetString());
    }

    [Fact]
    public async Task ProviderQuotaIsReportedAsRateLimitedAndInvalidCredentialsAsConfigurationError()
    {
        using var quotaClient = CreateHttpClient(new DelegateHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests))));
        var quotaProvider = new GeminiAssistantLlmClient(quotaClient, Options.Create(new AssistantAiOptions
        {
            ApiKey = "unit-test-key"
        }));
        await Assert.ThrowsAsync<AssistantProviderRateLimitException>(() => quotaProvider.GenerateAsync(Request("Hello")));

        using var invalidKeyClient = CreateHttpClient(new DelegateHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))));
        var invalidKeyProvider = new GeminiAssistantLlmClient(invalidKeyClient, Options.Create(new AssistantAiOptions
        {
            ApiKey = "unit-test-key"
        }));
        await Assert.ThrowsAsync<AssistantConfigurationException>(() => invalidKeyProvider.GenerateAsync(Request("Hello")));
    }

    [Fact]
    public async Task NetworkFailureWhileReadingProviderResponseIsMappedToProviderException()
    {
        using var httpClient = CreateHttpClient(new DelegateHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new FailingContent() })));
        var provider = new GeminiAssistantLlmClient(httpClient, Options.Create(new AssistantAiOptions
        {
            ApiKey = "unit-test-key"
        }));

        await Assert.ThrowsAsync<AssistantProviderException>(() => provider.GenerateAsync(Request("Hello")));
    }

    [Fact]
    public async Task InvalidJsonFromProviderIsMappedToProviderException()
    {
        using var httpClient = CreateHttpClient(new DelegateHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-json", Encoding.UTF8, "application/json")
            })));
        var provider = new GeminiAssistantLlmClient(httpClient, Options.Create(new AssistantAiOptions
        {
            ApiKey = "unit-test-key"
        }));

        await Assert.ThrowsAsync<AssistantProviderException>(() => provider.GenerateAsync(Request("Hello")));
    }

    [Fact]
    public async Task ProviderTimeoutRaisesSpecificTimeoutException()
    {
        var handler = new DelegateHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = new GeminiAssistantLlmClient(httpClient, Options.Create(new AssistantAiOptions
        {
            ApiKey = "unit-test-key",
            TimeoutSeconds = 1
        }));

        await Assert.ThrowsAsync<AssistantProviderTimeoutException>(() => provider.GenerateAsync(Request("Hello")));
    }

    [Fact]
    public async Task CallerCancellationRemainsCancellationRatherThanProviderTimeout()
    {
        var handler = new DelegateHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = CreateHttpClient(handler);
        var provider = new GeminiAssistantLlmClient(httpClient, Options.Create(new AssistantAiOptions
        {
            ApiKey = "unit-test-key",
            TimeoutSeconds = 30
        }));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GenerateAsync(Request("Hello"), cancellation.Token));
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
        Timeout = Timeout.InfiniteTimeSpan
    };

    private static AssistantLlmRequest Request(string content) => new(
        "You are SkyMate.",
        [new AssistantLlmTurn(ConversationRole.User, content)]);

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return send(request, cancellationToken);
        }
    }

    private sealed class FailingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            Task.FromException(new HttpRequestException("Mock response stream failed."));

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
