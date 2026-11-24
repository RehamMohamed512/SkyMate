namespace SkyMate.Application.AI;

/// <summary>Only the trip fields needed to answer the current message; never persisted on a conversation.</summary>
public sealed record AssistantTripContext(
    string Destination,
    DateOnly StartDate,
    int DaysNumber,
    int TravellersCount,
    string Pace,
    IReadOnlyList<string> Interests,
    decimal Budget,
    string Currency,
    IReadOnlyList<AssistantTripDayContext> Days,
    decimal? ActivitiesTotal);

/// <summary>Backend-calculated itinerary data for prompt context.</summary>
public sealed record AssistantTripDayContext(
    int DayNumber,
    DateOnly Date,
    string WeekDay,
    IReadOnlyList<AssistantTripActivityContext> Activities,
    decimal? ActivitiesTotal);

/// <summary>Prompt-safe activity data; TotalForGroup is calculated by the backend.</summary>
public sealed record AssistantTripActivityContext(
    int ActivityOrder,
    string Name,
    string? Description,
    string? LocationName,
    string ActivityType,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    decimal EstimatedCost,
    string CostBasis,
    decimal? TotalForGroup);

/// <summary>A single active knowledge-base item exposed to prompt construction.</summary>
public sealed record AssistantKnowledgeBaseItem(string Title, string Content);

/// <summary>Dynamic, per-request material added to the original AI-team system instructions.</summary>
public sealed record AssistantPromptContext(
    IReadOnlyList<AssistantKnowledgeBaseItem> KnowledgeBase,
    AssistantTripContext? CurrentTrip,
    bool AllowGeneralTravelQuestions);
