using SkyMate.Domain.Enums;

namespace SkyMate.API.DTOs.Trips;

/// <summary>Trip fields exposed by the read endpoints.</summary>
public sealed record TripResponse(
    int Id,
    string Destination,
    decimal Budget,
    string Currency,
    int DaysNumber,
    int TravellersCount,
    DateOnly StartDate,
    Pace Pace);
