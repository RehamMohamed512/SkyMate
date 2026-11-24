using SkyMate.Domain.Enums;

namespace SkyMate.Application.AI;

/// <summary>A text turn supplied to a replaceable assistant model client.</summary>
public sealed record AssistantLlmTurn(
    ConversationRole Role,
    string Content,
    string? ProviderContextJson = null);

/// <summary>A provider-neutral request containing system guidance and local conversation history.</summary>
public sealed record AssistantLlmRequest(
    string SystemInstruction,
    IReadOnlyList<AssistantLlmTurn> Messages);

/// <summary>A provider-neutral text response.</summary>
public sealed record AssistantLlmResponse(string Text, string? ProviderContextJson = null);
