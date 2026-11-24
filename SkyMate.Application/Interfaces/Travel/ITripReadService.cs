using SkyMate.Application.DTOs.Trips;

namespace SkyMate.Application.Interfaces.Travel;

/// <summary>Defines read operations for trips owned by a user.</summary>
public interface ITripReadService
{
    /// <summary>Lists trip summaries for the specified user.</summary>
    Task<IReadOnlyList<TripSummary>> ListForUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Gets a trip only when it is owned by the specified user.</summary>
    /// <returns>The trip summary, or <see langword="null"/> if it is unavailable.</returns>
    Task<TripSummary?> GetForUserAsync(int tripId, int userId, CancellationToken cancellationToken = default);
}
