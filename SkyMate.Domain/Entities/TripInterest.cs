namespace SkyMate.Domain.Entities;

/// <summary>Links a trip and an interest in their many-to-many relationship.</summary>
public sealed class TripInterest
{
    public int TripId { get; set; }
    public int InterestId { get; set; }

    public Trip Trip { get; set; } = null!;
    public Interest Interest { get; set; } = null!;
}
