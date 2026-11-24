namespace SkyMate.Domain.Entities;

/// <summary>Represents a travel preference saved for a user.</summary>
public sealed class UserPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    /// <summary>The preference category, such as a travel style.</summary>
    public string Type { get; set; } = string.Empty;
    public string Interest { get; set; } = string.Empty;
    public string? Pace { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
