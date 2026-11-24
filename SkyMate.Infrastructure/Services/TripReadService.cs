using Microsoft.EntityFrameworkCore;
using SkyMate.Application.DTOs.Trips;
using SkyMate.Application.Interfaces.Travel;
using SkyMate.Infrastructure.Persistence;

namespace SkyMate.Infrastructure.Services;

/// <summary>Reads trip summaries with EF Core, filtering by owner in the database.</summary>
public sealed class TripReadService(AppDbContext dbContext) : ITripReadService
{
    /// <summary>Lists the user's trips, ordered by start date from newest to oldest.</summary>
    public async Task<IReadOnlyList<TripSummary>> ListForUserAsync(
        int userId,
        CancellationToken cancellationToken = default) => await dbContext.Trips
        .AsNoTracking()
        .Where(trip => trip.UserId == userId)
        .OrderByDescending(trip => trip.StartDate)
        .Select(trip => new TripSummary(
            trip.Id,
            trip.Destination,
            trip.Budget,
            trip.Currency,
            trip.DaysNumber,
            trip.TravellersCount,
            trip.StartDate,
            trip.Pace))
        .ToListAsync(cancellationToken);

    /// <summary>Gets a trip only when it belongs to the specified user.</summary>
    /// <returns>The trip summary, or <see langword="null"/> if it is unavailable.</returns>
    public Task<TripSummary?> GetForUserAsync(
        int tripId,
        int userId,
        CancellationToken cancellationToken = default) => dbContext.Trips
        .AsNoTracking()
        .Where(trip => trip.Id == tripId && trip.UserId == userId)
        .Select(trip => new TripSummary(
            trip.Id,
            trip.Destination,
            trip.Budget,
            trip.Currency,
            trip.DaysNumber,
            trip.TravellersCount,
            trip.StartDate,
            trip.Pace))
        .SingleOrDefaultAsync(cancellationToken);
}
