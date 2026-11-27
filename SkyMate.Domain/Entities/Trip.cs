using SkyMate.Domain.Enums;

namespace SkyMate.Domain.Entities;

/// <summary>Represents a user's trip, its dates, budget, and itinerary.</summary>
public sealed class Trip
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Destination { get; set; } = string.Empty;
    /// <summary>The estimated budget; values must be zero or greater.</summary>
    public decimal Budget { get; set; }
    public string Currency { get; set; } = "USD";
    /// <summary>The trip duration in days. The database allows values from 1 to 30.</summary>
    public int DaysNumber { get; set; }
    /// <summary>The number of travelers; it must be at least 1.</summary>
    public int TravellersCount { get; set; }
    /// <summary>The trip start date, stored without a time or time zone.</summary>
    public DateOnly StartDate { get; set; }
    public Pace Pace { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<TripDay> Days { get; set; } = new List<TripDay>();
    public ICollection<TripInterest> TripInterests { get; set; } = new List<TripInterest>();
    /// <summary>Conversations associated with this trip.</summary>
    public ICollection<AiConversation> Conversations { get; set; } = new List<AiConversation>();

    /// <summary>
    /// Checks whether the trip is active on a date. The start date is included and the
    /// day after the final trip day is the exclusive end date.
    /// </summary>
    /// <param name="date">The date to check.</param>
    /// <returns><see langword="true"/> when the date falls within the trip.</returns>
    public bool IsActiveOn(DateOnly date) => StartDate <= date && date < StartDate.AddDays(DaysNumber);

    /// <summary>Checks whether the trip's exclusive end date has arrived.</summary>
    /// <param name="date">The date to check.</param>
    /// <returns><see langword="true"/> when the trip has ended on or before this date.</returns>
    public bool HasEndedOn(DateOnly date) => StartDate.AddDays(DaysNumber) <= date;
}
