namespace SkyMate.Domain.Entities;

/// <summary>Represents one day in a trip itinerary and its planned activities.</summary>
public sealed class TripDay
{
    public int Id { get; set; }
    public int TripId { get; set; }
    /// <summary>The day's position in the trip, starting at 1.</summary>
    public int DayNumber { get; set; }
    public DateOnly Date { get; set; }
    public string WeekDay { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Trip Trip { get; set; } = null!;
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
