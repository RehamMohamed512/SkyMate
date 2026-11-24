using SkyMate.Domain.Enums;

namespace SkyMate.Application.DTOs.Trips;

/// <summary>Contains the trip fields needed by the API without exposing the full entity.</summary>
public sealed record TripSummary(
    int Id,
    string Destination,
    decimal Budget,
    string Currency,
    int DaysNumber,
    int TravellersCount,
    DateOnly StartDate,
    Pace Pace);
