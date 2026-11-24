using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkyMate.API.DTOs.Trips;
using SkyMate.API.Security;
using SkyMate.Application.DTOs.Trips;
using SkyMate.Application.Interfaces.Travel;

namespace SkyMate.API.Controllers;

/// <summary>Provides read endpoints for trips owned by the authenticated user.</summary>
/// <param name="tripReadService">Reads trips filtered by owner.</param>
[ApiController]
[Route("api/v1/trips")]
[Authorize(Roles = "User")]
public sealed class TripsController(ITripReadService tripReadService) : ControllerBase
{
    /// <summary>Lists the current user's trips.</summary>
    /// <returns>The trip list, or Unauthorized when the token has no valid user ID.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TripResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var trips = await tripReadService.ListForUserAsync(userId.Value, cancellationToken);
        return Ok(trips.Select(ToResponse).ToArray());
    }

    /// <summary>Gets a trip only when it belongs to the current user.</summary>
    /// <param name="tripId">The trip ID from the route.</param>
    /// <returns>The trip, or Not Found when it is missing or belongs to another user.</returns>
    [HttpGet("{tripId:int}")]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int tripId, CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        // Map to an API response instead of exposing the entity and its relationships.
        var trip = await tripReadService.GetForUserAsync(tripId, userId.Value, cancellationToken);
        return trip is null ? NotFound() : Ok(ToResponse(trip));
    }

    private static TripResponse ToResponse(TripSummary trip) => new(
        trip.Id,
        trip.Destination,
        trip.Budget,
        trip.Currency,
        trip.DaysNumber,
        trip.TravellersCount,
        trip.StartDate,
        trip.Pace);
}
