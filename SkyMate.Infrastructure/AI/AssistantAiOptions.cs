namespace SkyMate.Infrastructure.AI;

/// <summary>Settings for the replaceable travel-assistant provider integration.</summary>
public sealed class AssistantAiOptions
{
    public const string SectionName = "AssistantAi";

    /// <summary>Secret provider credential. Configure outside source control.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gemini Interactions API model identifier.</summary>
    public string Model { get; set; } = "gemini-3.5-flash-lite";

    /// <summary>Models tried in order when the primary model is temporarily unavailable.</summary>
    public string[] FallbackModels { get; set; } = [];

    /// <summary>Maximum number of earlier messages supplied as history.</summary>
    public int MaxHistoryMessages { get; set; } = 10;

    /// <summary>Provider request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Whether clearly labeled general travel advice is permitted.</summary>
    public bool AllowGeneralTravelQuestions { get; set; }
}
