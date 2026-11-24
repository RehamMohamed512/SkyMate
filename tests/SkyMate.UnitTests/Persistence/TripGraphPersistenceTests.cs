using Microsoft.EntityFrameworkCore;
using SkyMate.Domain.Entities;
using SkyMate.Domain.Enums;

namespace SkyMate.UnitTests.Persistence;

/// <summary>Tests saving a trip and its related entities through AppDbContext.</summary>
public sealed class TripGraphPersistenceTests
{
    /// <summary>Checks that a trip, its days, activities, and interests save with valid relationships.</summary>
    [Fact]
    public async Task SavesTripDaysActivitiesAndInterestsInOneSave()
    {
        var (connection, context) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (context)
        {
            var user = AppDbContextTests.NewUser("traveler@example.com");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var interest = new Interest { Name = "  Culture  " };
            var trip = new Trip
            {
                UserId = user.Id,
                Destination = "Rome",
                Budget = 1250.50m,
                Currency = "EUR",
                DaysNumber = 1,
                TravellersCount = 2,
                StartDate = new DateOnly(2026, 12, 10),
                Pace = Pace.Balanced,
                Days =
                [
                    new TripDay
                    {
                        DayNumber = 1,
                        Date = new DateOnly(2026, 12, 10),
                        WeekDay = "Thursday",
                        Activities =
                        [
                            new Activity
                            {
                                Name = "Colosseum",
                                ActivityType = ActivityType.Sightseeing,
                                EstimatedCost = 30,
                                CostBasis = CostBasis.PerPerson,
                                ActivityOrder = 1
                            }
                        ]
                    }
                ],
                TripInterests = [new TripInterest { Interest = interest }]
            };

            context.Trips.Add(trip);
            var rowsChanged = await context.SaveChangesAsync();

            Assert.True(rowsChanged >= 5);
            Assert.Equal(1, await context.Trips.CountAsync());
            Assert.Equal(1, await context.TripDays.CountAsync());
            Assert.Equal(1, await context.Activities.CountAsync());
            Assert.Equal("Culture", (await context.Interests.SingleAsync()).Name);
            Assert.Equal(1, await context.TripInterests.CountAsync());
            Assert.Equal(Pace.Balanced, await context.Trips.Select(item => item.Pace).SingleAsync());
        }
    }
}
