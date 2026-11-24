namespace SkyMate.API.DTOs.Chatbot;

/// <summary>Administrator-managed fields for a knowledge-base entry.</summary>
public sealed class KnowledgeBaseEntryRequest
{
    public string? Title { get; init; }
    public string? Content { get; init; }
    public bool IsActive { get; init; } = true;
}
