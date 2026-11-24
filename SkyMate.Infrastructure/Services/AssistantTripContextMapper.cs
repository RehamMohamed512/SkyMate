using SkyMate.Application.AI;
using SkyMate.Domain.Entities;
using SkyMate.Domain.Enums;

namespace SkyMate.Infrastructure.Services;

/// <summary>Projects only the itinerary fields needed by the assistant and calculates all supplied totals.</summary>
internal static class AssistantTripContextMapper
{
    public static AssistantTripContext Map(Trip trip)
    {
        var days = trip.Days
            .OrderBy(day => day.DayNumber)
            .Select(day =>
            {
                var activities = day.Activities
                    .OrderBy(activity => activity.ActivityOrder)
                    .Select(activity => new AssistantTripActivityContext(
                        activity.ActivityOrder,
                        activity.Name,
                        activity.Description,
                        activity.LocationName,
                        activity.ActivityType.ToString(),
                        activity.StartTime,
                        activity.EndTime,
                        activity.EstimatedCost,
                        activity.CostBasis.ToString(),
                        CalculateActivityTotal(activity, trip.TravellersCount)))
                    .ToArray();

                return new AssistantTripDayContext(
                    day.DayNumber,
                    day.Date,
                    day.WeekDay,
                    activities,
                    Sum(activities.Select(activity => activity.TotalForGroup)));
            })
            .ToArray();

        return new AssistantTripContext(
            trip.Destination,
            trip.StartDate,
            trip.DaysNumber,
            trip.TravellersCount,
            trip.Pace.ToString(),
            trip.TripInterests
                .Select(link => link.Interest.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            trip.Budget,
            trip.Currency,
            days,
            Sum(days.Select(day => day.ActivitiesTotal)));
    }

    private static decimal? CalculateActivityTotal(Activity activity, int travellersCount)
    {
        try
        {
            return activity.CostBasis switch
            {
                CostBasis.PerPerson => checked(activity.EstimatedCost * travellersCount),
                CostBasis.PerGroup => activity.EstimatedCost,
                CostBasis.Free => 0m,
                _ => null
            };
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static decimal? Sum(IEnumerable<decimal?> values)
    {
        var amounts = values.ToArray();
        if (amounts.Any(amount => !amount.HasValue))
        {
            return null;
        }

        try
        {
            return amounts.Aggregate(0m, (total, amount) => checked(total + amount!.Value));
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}
