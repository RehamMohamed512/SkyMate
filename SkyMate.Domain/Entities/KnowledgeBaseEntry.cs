namespace SkyMate.Domain.Entities;

/// <summary>Editable assistant knowledge that is included in prompts when active.</summary>
public sealed class KnowledgeBaseEntry
{
    public int Id { get; set; }
    /// <summary>Stable identity for an original seed entry; null for administrator-created entries.</summary>
    public string? SeedKey { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
