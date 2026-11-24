using SkyMate.Domain.Enums;

namespace SkyMate.Domain.Entities;

/// <summary>Represents one planned activity within a trip day.</summary>
public sealed class Activity
{
    public int Id { get; set; }
    public int DayId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LocationName { get; set; }
    public ActivityType ActivityType { get; set; }

    /// <summary>The estimated cost. The database rejects negative values.</summary>
    public decimal EstimatedCost { get; set; }
    public CostBasis CostBasis { get; set; }
    /// <summary>The optional start time within the day, without a date or time zone.</summary>
    public TimeOnly? StartTime { get; set; }
    /// <summary>The optional end time within the day, without a date or time zone.</summary>
    public TimeOnly? EndTime { get; set; }
    /// <summary>The activity's position within the day, starting at 1.</summary>
    public int ActivityOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TripDay Day { get; set; } = null!;
}
