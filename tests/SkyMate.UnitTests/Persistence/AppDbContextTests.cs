using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SkyMate.Domain.Entities;
using SkyMate.Domain.Enums;
using SkyMate.Infrastructure.Identity;

namespace SkyMate.UnitTests.Persistence;

/// <summary>Tests the EF Core model, constraints, and relationships configured by AppDbContext.</summary>
public sealed class AppDbContextTests
{
    /// <summary>Checks that Identity and SkyMate tables are present in the EF model.</summary>
    [Fact]
    public async Task CreatesIdentityAndApplicationTables()
    {
        var (connection, context) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (context)
        {
            var tables = await context.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync();
            var expected = new[]
            {
                "AspNetUsers", "AspNetRoles", "AspNetUserRoles", "AspNetUserClaims", "AspNetUserLogins",
                "AspNetRoleClaims", "AspNetUserTokens", "UserPreferences", "Interests", "Trips",
                "TripDays", "Activities", "TripInterests", "AiConversations", "AiConversationMessages",
                "KnowledgeBaseEntries"
            };

            Assert.All(expected, table => Assert.Contains(table, tables));
            Assert.DoesNotContain("UserPhones", tables);
        }
    }

    /// <summary>Checks keys, indexes, delete behaviors, and enum string conversions.</summary>
    [Fact]
    public async Task ConfiguresKeysDeleteBehaviorsIndexesAndStringEnumConversions()
    {
        var (connection, context) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (context)
        {
            var tripType = context.Model.FindEntityType(typeof(Trip))!;
            Assert.Contains(tripType.GetIndexes(), index => !index.IsUnique &&
                index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(Trip.UserId), nameof(Trip.StartDate) }));

            var tripDayType = context.Model.FindEntityType(typeof(TripDay))!;
            Assert.Contains(tripDayType.GetIndexes(), index => index.IsUnique &&
                index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(TripDay.TripId), nameof(TripDay.DayNumber) }));
            Assert.Contains(tripDayType.GetIndexes(), index => index.IsUnique &&
                index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(TripDay.TripId), nameof(TripDay.Date) }));

            var tripInterestType = context.Model.FindEntityType(typeof(TripInterest))!;
            Assert.Equal(new[] { nameof(TripInterest.TripId), nameof(TripInterest.InterestId) },
                tripInterestType.FindPrimaryKey()!.Properties.Select(property => property.Name));

            var tripPace = tripType.FindProperty(nameof(Trip.Pace))!;
            Assert.Equal(typeof(string), tripPace.GetTypeMapping().Converter!.ProviderClrType);
            var activityType = context.Model.FindEntityType(typeof(Activity))!;
            Assert.Contains(activityType.GetIndexes(), index => index.IsUnique &&
                index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(Activity.DayId), nameof(Activity.ActivityOrder) }));
            Assert.Equal(DeleteBehavior.Cascade, tripType.GetForeignKeys()
                .Single(foreignKey => foreignKey.Properties.Any(property => property.Name == nameof(Trip.UserId))).DeleteBehavior);
            Assert.Equal(DeleteBehavior.Cascade, tripDayType.GetForeignKeys()
                .Single(foreignKey => foreignKey.Properties.Any(property => property.Name == nameof(TripDay.TripId))).DeleteBehavior);

            var conversationType = context.Model.FindEntityType(typeof(AiConversation))!;
            Assert.Contains(conversationType.GetIndexes(), index =>
                index.Properties.Select(property => property.Name).SequenceEqual(new[]
                {
                    nameof(AiConversation.UserId), nameof(AiConversation.UpdatedAt)
                }));
            Assert.Contains(conversationType.GetIndexes(), index =>
                index.Properties.Select(property => property.Name).SequenceEqual(new[]
                {
                    nameof(AiConversation.TripId), nameof(AiConversation.UserId), nameof(AiConversation.CreatedAt)
                }));
            Assert.Equal(DeleteBehavior.Cascade, conversationType.GetForeignKeys()
                .Single(foreignKey => foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual(new[] { nameof(AiConversation.UserId) })).DeleteBehavior);
            Assert.Equal(typeof(ApplicationUser), conversationType.GetForeignKeys()
                .Single(foreignKey => foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual(new[] { nameof(AiConversation.UserId) }))
                .PrincipalEntityType.ClrType);
            var conversationTripForeignKey = conversationType.GetForeignKeys()
                .Single(foreignKey => foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual(new[] { nameof(AiConversation.TripId), nameof(AiConversation.UserId) }));
            Assert.Equal(typeof(Trip), conversationTripForeignKey.PrincipalEntityType.ClrType);
            Assert.Equal(DeleteBehavior.ClientNoAction, conversationTripForeignKey.DeleteBehavior);
            var messageType = context.Model.FindEntityType(typeof(AiConversationMessage))!;
            Assert.Equal(DeleteBehavior.Cascade, messageType.GetForeignKeys()
                .Single(foreignKey => foreignKey.Properties.Any(property => property.Name == nameof(AiConversationMessage.ConversationId)))
                .DeleteBehavior);
        }
    }

    /// <summary>Checks that a trip cannot be deleted while a retained conversation references it.</summary>
    [Fact]
    public async Task ConversationTripDependencyPreventsDeletingRetainedConversationHistory()
    {
        var (connection, context) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (context)
        {
            var owner = NewUser("trip-owner@example.com");
            context.Users.Add(owner);
            await context.SaveChangesAsync();

            var trip = NewTrip(owner.Id);
            context.Trips.Add(trip);
            await context.SaveChangesAsync();

            var conversation = new AiConversation
            {
                UserId = owner.Id,
                TripId = trip.Id,
                Title = "Retained trip conversation",
                Messages = [new AiConversationMessage { Role = ConversationRole.User, Content = "Saved with its trip." }]
            };
            context.AiConversations.Add(conversation);
            await context.SaveChangesAsync();

            context.Trips.Remove(trip);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

            Assert.True(await context.AiConversations.AnyAsync(item => item.Id == conversation.Id));
            Assert.True(await context.AiConversationMessages.AnyAsync(item => item.ConversationId == conversation.Id));
        }
    }

    /// <summary>Checks unique email, preference, interest, and trip-day constraints.</summary>
    [Fact]
    public async Task EnforcesUniqueEmailPreferenceInterestAndTripDayConstraints()
    {
        var (connection, context) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (context)
        {
            var user = NewUser("owner@example.com");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            context.Users.Add(NewUser("different@example.com", user.NormalizedEmail));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();

            context.UserPreferences.AddRange(
                new UserPreference { UserId = user.Id, Type = "Travel", Interest = "Art" },
                new UserPreference { UserId = user.Id, Type = "Travel", Interest = "Art" });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();

            context.Interests.AddRange(new Interest { Name = "Nature" }, new Interest { Name = "nature" });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();

            var trip = NewTrip(user.Id);
            context.Trips.Add(trip);
            await context.SaveChangesAsync();
            context.TripDays.AddRange(
                new TripDay { TripId = trip.Id, DayNumber = 1, Date = trip.StartDate, WeekDay = "Tuesday" },
                new TripDay { TripId = trip.Id, DayNumber = 1, Date = trip.StartDate, WeekDay = "Tuesday" });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }

    /// <summary>Checks that invalid trip values are rejected by database constraints.</summary>
    [Fact]
    public async Task EnforcesRequiredTripFieldsAndBusinessChecks()
    {
        var (connection, context) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (context)
        {
            var user = NewUser("checks@example.com");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var missingDestination = NewTrip(user.Id);
            missingDestination.Destination = null!;
            context.Trips.Add(missingDestination);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();

            var tooManyDays = NewTrip(user.Id);
            tooManyDays.DaysNumber = 31;
            context.Trips.Add(tooManyDays);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();

            var negativeBudget = NewTrip(user.Id);
            negativeBudget.Budget = -0.01m;
            context.Trips.Add(negativeBudget);
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }

    /// <summary>Allows separate trips with the same destination and start date.</summary>
    [Fact]
    public async Task AllowsDuplicateDestinationAndStartDateForSameUser()
    {
        var (connection, context) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (context)
        {
            var user = NewUser("repeat-trip@example.com");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var firstTrip = NewTrip(user.Id);
            var secondTrip = NewTrip(user.Id);
            secondTrip.Budget = firstTrip.Budget + 100;
            context.Trips.AddRange(firstTrip, secondTrip);
            await context.SaveChangesAsync();

            Assert.Equal(2, await context.Trips.CountAsync(trip =>
                trip.UserId == user.Id && trip.Destination == firstTrip.Destination &&
                trip.StartDate == firstTrip.StartDate));
        }
    }

    /// <summary>Enforces activity order within a day while allowing the same order on another day.</summary>
    [Fact]
    public async Task ActivityOrderIsUniquePerDay()
    {
        var (connection, context) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (context)
        {
            var user = NewUser("activity-order@example.com");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var trip = NewTrip(user.Id);
            trip.DaysNumber = 2;
            trip.Days =
            [
                new TripDay { DayNumber = 1, Date = trip.StartDate, WeekDay = "Tuesday" },
                new TripDay { DayNumber = 2, Date = trip.StartDate.AddDays(1), WeekDay = "Wednesday" }
            ];
            context.Trips.Add(trip);
            await context.SaveChangesAsync();

            var firstDay = trip.Days.OrderBy(day => day.DayNumber).First();
            context.Activities.AddRange(
                new Activity { DayId = firstDay.Id, Name = "First", ActivityOrder = 1 },
                new Activity { DayId = firstDay.Id, Name = "Duplicate", ActivityOrder = 1 });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();

            var days = await context.TripDays.OrderBy(day => day.DayNumber).ToListAsync();
            context.Activities.AddRange(
                new Activity { DayId = days[0].Id, Name = "Day one", ActivityOrder = 1 },
                new Activity { DayId = days[1].Id, Name = "Day two", ActivityOrder = 1 });
            await context.SaveChangesAsync();
        }
    }

    /// <summary>Rejects overlapping scheduled activities but allows activities without times.</summary>
    [Fact]
    public async Task RejectsOverlappingScheduledActivities()
    {
        var (connection, context) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (context)
        {
            var user = NewUser("activity-overlap@example.com");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var trip = NewTrip(user.Id);
            trip.DaysNumber = 1;
            trip.Days = [new TripDay { DayNumber = 1, Date = trip.StartDate, WeekDay = "Tuesday" }];
            context.Trips.Add(trip);
            await context.SaveChangesAsync();

            var day = trip.Days.Single();
            context.Activities.Add(new Activity
            {
                DayId = day.Id,
                Name = "First",
                ActivityOrder = 1,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0)
            });
            await context.SaveChangesAsync();

            context.Activities.Add(new Activity
            {
                DayId = day.Id,
                Name = "Overlapping",
                ActivityOrder = 2,
                StartTime = new TimeOnly(9, 30),
                EndTime = new TimeOnly(10, 30)
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        }
    }

    /// <summary>Deletes all user-owned data through database cascade relationships.</summary>
    [Fact]
    public async Task DeletingUserDeletesOwnedTripsPreferencesAndConversationMessages()
    {
        var (connection, context) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (context)
        {
            var user = NewUser("account-delete@example.com");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var trip = NewTrip(user.Id);
            trip.DaysNumber = 1;
            trip.Days =
            [
                new TripDay
                {
                    DayNumber = 1,
                    Date = trip.StartDate,
                    WeekDay = "Tuesday",
                    Activities = [new Activity { Name = "Museum", ActivityOrder = 1 }]
                }
            ];
            trip.TripInterests = [new TripInterest { Interest = new Interest { Name = "Art" } }];
            context.Trips.Add(trip);
            user.AiConversations =
            [
                new AiConversation
                {
                    Title = "Independent conversation",
                    Messages =
                    [
                        new AiConversationMessage
                        {
                            Role = ConversationRole.User,
                            Content = "Plan my day"
                        },
                        new AiConversationMessage
                        {
                            Role = ConversationRole.Assistant,
                            Content = "Where would you like to go?"
                        }
                    ]
                }
            ];
            context.UserPreferences.Add(new UserPreference { UserId = user.Id, Type = "Travel", Interest = "Art" });
            context.KnowledgeBaseEntries.Add(new KnowledgeBaseEntry
            {
                Title = "Shared assistant knowledge",
                Content = "Not owned by an account."
            });
            context.Roles.Add(new IdentityRole<int> { Name = "User", NormalizedName = "USER" });
            context.UserClaims.Add(new IdentityUserClaim<int>
            {
                UserId = user.Id,
                ClaimType = "test",
                ClaimValue = "claim"
            });
            context.UserLogins.Add(new IdentityUserLogin<int>
            {
                UserId = user.Id,
                LoginProvider = "test-provider",
                ProviderKey = "test-key"
            });
            context.UserTokens.Add(new IdentityUserToken<int>
            {
                UserId = user.Id,
                LoginProvider = "test-provider",
                Name = "test-token",
                Value = "test-value"
            });
            await context.SaveChangesAsync();

            var role = await context.Roles.SingleAsync();
            context.UserRoles.Add(new IdentityUserRole<int> { UserId = user.Id, RoleId = role.Id });
            await context.SaveChangesAsync();

            // Delete an unloaded user so SQLite's actual foreign keys, rather than tracked
            // navigation fix-up, must remove every dependent row.
            context.ChangeTracker.Clear();
            var userToDelete = await context.Users.SingleAsync(item => item.Id == user.Id);
            context.Users.Remove(userToDelete);
            await context.SaveChangesAsync();

            Assert.Equal(0, await context.Users.CountAsync());
            Assert.Equal(0, await context.UserPreferences.CountAsync());
            Assert.Equal(0, await context.Trips.CountAsync());
            Assert.Equal(0, await context.TripDays.CountAsync());
            Assert.Equal(0, await context.Activities.CountAsync());
            Assert.Equal(0, await context.TripInterests.CountAsync());
            Assert.Equal(0, await context.AiConversations.CountAsync());
            Assert.Equal(0, await context.AiConversationMessages.CountAsync());
            Assert.Equal(0, await context.UserClaims.CountAsync());
            Assert.Equal(0, await context.UserLogins.CountAsync());
            Assert.Equal(0, await context.UserTokens.CountAsync());
            Assert.Equal(0, await context.UserRoles.CountAsync());
            Assert.Equal(1, await context.KnowledgeBaseEntries.CountAsync());
            Assert.Equal(1, await context.Roles.CountAsync());
            Assert.Equal(1, await context.Interests.CountAsync());
        }
    }

    /// <summary>Creates an unsaved Identity user for persistence tests.</summary>
    internal static ApplicationUser NewUser(string email, string? normalizedEmail = null) => new()
    {
        UserName = email,
        NormalizedUserName = email.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = normalizedEmail ?? email.ToUpperInvariant(),
        FirstName = "Test",
        LastName = "User"
    };

    /// <summary>Creates a valid unsaved trip for persistence tests.</summary>
    internal static Trip NewTrip(int userId) => new()
    {
        UserId = userId,
        Destination = "Cairo",
        Budget = 200,
        DaysNumber = 2,
        TravellersCount = 1,
        StartDate = new DateOnly(2026, 12, 1),
        Pace = Pace.Balanced
    };
}
