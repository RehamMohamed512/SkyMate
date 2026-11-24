namespace SkyMate.Application.AI;

/// <summary>Base exception for expected assistant integration failures.</summary>
public abstract class AssistantIntegrationException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>Indicates required provider configuration, such as an API key, is missing.</summary>
public sealed class AssistantConfigurationException(string message)
    : AssistantIntegrationException(message);

/// <summary>Indicates that an AI provider request failed or returned an unusable response.</summary>
public sealed class AssistantProviderException(string message, Exception? innerException = null)
    : AssistantIntegrationException(message, innerException);

/// <summary>Indicates that the provider did not respond within the configured timeout.</summary>
public sealed class AssistantProviderTimeoutException(string message, Exception? innerException = null)
    : AssistantIntegrationException(message, innerException);

/// <summary>Indicates the external AI provider's quota or per-key rate limit was reached.</summary>
public sealed class AssistantProviderRateLimitException(string message)
    : AssistantIntegrationException(message);

/// <summary>Signals that a knowledge-base title is already in use.</summary>
public sealed class KnowledgeBaseTitleConflictException : Exception
{
}
