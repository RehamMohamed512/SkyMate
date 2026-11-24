using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SkyMate.API.DTOs.Chatbot;
using SkyMate.API.DTOs.Auth;
using SkyMate.Application.DTOs.Chatbot;
using SkyMate.Domain.Entities;
using SkyMate.Domain.Enums;
using SkyMate.Infrastructure.Persistence;
using SkyMate.Infrastructure.Identity;
using SkyMate.UnitTests.Authentication;

namespace SkyMate.UnitTests.Chatbot;

/// <summary>HTTP and persistence tests for user-owned, trip-independent conversations.</summary>
public sealed class ChatbotLifecycleAndAuthorizationTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly AuthApiFactory _factory = new();
    private readonly HttpClient _client;

    public ChatbotLifecycleAndAuthorizationTests() => _client = _factory.CreateClient();

    [Fact]
    public async Task UserCanCreateConversationsWithoutTripsAndListThemByUpdatedAt()
    {
        await RegisterAsync("no-trips@example.com");
        await LoginAsync("no-trips@example.com");

        var first = await CreateConversationAsync("First conversation");
        var second = await CreateConversationAsync("Second conversation");
        Assert.NotEqual(first.Id, second.Id);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var firstEntity = await context.AiConversations.SingleAsync(item => item.Id == first.Id);
            firstEntity.UpdatedAt = DateTime.UtcNow.AddMinutes(5);
            await context.SaveChangesAsync();
        }

        var listResponse = await _client.GetAsync("/api/v1/conversations");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var conversations = await listResponse.Content.ReadFromJsonAsync<ConversationResponse[]>(JsonOptions);
        Assert.NotNull(conversations);
        Assert.Equal(first.Id, conversations[0].Id);
        Assert.Equal(second.Id, conversations[1].Id);
        Assert.DoesNotContain("tripId", await listResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        using var readScope = _factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await readContext.AiConversations.CountAsync());
        Assert.Empty(await readContext.AiConversationMessages.ToListAsync());
    }

    [Fact]
    public async Task SendingMessagePersistsBothRolesAndUsesSavedHistory()
    {
        await RegisterAsync("history@example.com");
        await LoginAsync("history@example.com");
        var conversation = await CreateConversationAsync("History");

        var firstResponse = await _client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = "Suggest a quiet city break." });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var firstTurn = await firstResponse.Content.ReadFromJsonAsync<ConversationTurnResponse>(JsonOptions);
        Assert.NotNull(firstTurn);
        Assert.Equal(ConversationRole.User, firstTurn.UserMessage.Role);
        Assert.Equal(ConversationRole.Assistant, firstTurn.AssistantMessage.Role);
        Assert.Equal("Travel reply: Suggest a quiet city break.", firstTurn.AssistantMessage.Content);

        var secondResponse = await _client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = "Keep it under two hours by train." });
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var messages = await context.AiConversationMessages
                .Where(item => item.ConversationId == conversation.Id)
                .OrderBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .ToListAsync();
            Assert.Equal(4, messages.Count);
            Assert.Equal(
                new[] { ConversationRole.User, ConversationRole.Assistant, ConversationRole.User, ConversationRole.Assistant },
                messages.Select(message => message.Role));
            var updatedAt = await context.AiConversations
                .Where(item => item.Id == conversation.Id)
                .Select(item => item.UpdatedAt)
                .SingleAsync();
            Assert.NotNull(updatedAt);
        }

        using var providerScope = _factory.Services.CreateScope();
        var provider = providerScope.ServiceProvider.GetRequiredService<TestAssistantLlmClient>();
        var request = provider.Requests.Last();
        Assert.Equal(3, request.Messages.Count);
        Assert.Equal(ConversationRole.User, request.Messages[0].Role);
        Assert.Equal(ConversationRole.Assistant, request.Messages[1].Role);
        Assert.Equal("Keep it under two hours by train.", request.Messages[2].Content);
        Assert.Contains("Reply in the same language", request.SystemInstruction, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendingMessageLimitsProviderContextToConfiguredRecentHistory()
    {
        var userId = await RegisterAsync("history-window@example.com");
        await LoginAsync("history-window@example.com");
        var conversation = await CreateConversationAsync("History limit");
        var firstMessageTime = DateTime.UtcNow.AddHours(-1);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var trip = AddTrip(userId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1));
            trip.Destination = "ConfidentialDestination";
            context.Trips.Add(trip);
            context.AiConversationMessages.AddRange(
                new AiConversationMessage
                {
                    ConversationId = conversation.Id,
                    Role = ConversationRole.User,
                    Content = "Old question outside history window",
                    CreatedAt = firstMessageTime
                },
                new AiConversationMessage
                {
                    ConversationId = conversation.Id,
                    Role = ConversationRole.Assistant,
                    Content = "Old answer outside history window",
                    CreatedAt = firstMessageTime.AddMinutes(1)
                },
                new AiConversationMessage
                {
                    ConversationId = conversation.Id,
                    Role = ConversationRole.User,
                    Content = "Recent question",
                    CreatedAt = firstMessageTime.AddMinutes(2)
                },
                new AiConversationMessage
                {
                    ConversationId = conversation.Id,
                    Role = ConversationRole.Assistant,
                    Content = "Recent answer",
                    CreatedAt = firstMessageTime.AddMinutes(3)
                });
            await context.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = "Current question" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var providerScope = _factory.Services.CreateScope();
        var provider = providerScope.ServiceProvider.GetRequiredService<TestAssistantLlmClient>();
        var request = provider.Requests.Last();
        Assert.Equal(
            new[] { "Recent question", "Recent answer", "Current question" },
            request.Messages.Select(message => message.Content));
        Assert.DoesNotContain(request.Messages, message => message.Content.StartsWith("Old ", StringComparison.Ordinal));
        var serializedRequest = JsonSerializer.Serialize(request);
        Assert.DoesNotContain("history-window@example.com", serializedRequest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConfidentialDestination", serializedRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TripContextIsOwnerCheckedAndOnlyAppliesToTheMessageThatSuppliedIt()
    {
        var ownerId = await RegisterAsync("trip-context-owner@example.com");
        await RegisterAsync("trip-context-other@example.com");
        await LoginAsync("trip-context-owner@example.com");
        var ownerConversation = await CreateConversationAsync("Trip questions");
        int tripId;

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var trip = AddTrip(ownerId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10));
            trip.Destination = "TripContextDestination";
            trip.DaysNumber = 2;
            trip.TravellersCount = 2;
            trip.Pace = Pace.Relaxed;
            trip.TripInterests.Add(new TripInterest { Interest = new Interest { Name = "Museums" } });
            trip.Days.Add(new TripDay
            {
                DayNumber = 1,
                Date = trip.StartDate,
                WeekDay = "Monday",
                Activities =
                [
                    new Activity
                    {
                        ActivityOrder = 1,
                        Name = "Quiet museum",
                        Description = "Museum note",
                        LocationName = "Old Town",
                        ActivityType = ActivityType.Culture,
                        EstimatedCost = 25,
                        CostBasis = CostBasis.PerPerson,
                        StartTime = new TimeOnly(10, 0),
                        EndTime = new TimeOnly(12, 0)
                    }
                ]
            });
            context.Trips.Add(trip);
            await context.SaveChangesAsync();
            tripId = trip.Id;
        }

        var tripMessage = await _client.PostAsJsonAsync(
            $"/api/v1/conversations/{ownerConversation.Id}/messages",
            new { content = "ما تكلفة المتحف؟", tripId });
        Assert.Equal(HttpStatusCode.Created, tripMessage.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider.GetRequiredService<TestAssistantLlmClient>();
            var request = provider.Requests.Last();
            Assert.Equal("ما تكلفة المتحف؟", request.Messages.Last().Content);
            Assert.Contains("TripContextDestination", request.SystemInstruction, StringComparison.Ordinal);
            Assert.Contains("\"pace\":\"Relaxed\"", request.SystemInstruction, StringComparison.Ordinal);
            Assert.Contains("\"interests\":[\"Museums\"]", request.SystemInstruction, StringComparison.Ordinal);
            Assert.Contains("\"totalForGroup\":50", request.SystemInstruction, StringComparison.Ordinal);
            Assert.DoesNotContain($"\"tripId\":{tripId}", request.SystemInstruction, StringComparison.Ordinal);
            Assert.Contains("Reply in the same language", request.SystemInstruction, StringComparison.Ordinal);
        }

        var englishMessage = await _client.PostAsJsonAsync(
            $"/api/v1/conversations/{ownerConversation.Id}/messages",
            new { content = "What does SkyMate do?" });
        Assert.Equal(HttpStatusCode.Created, englishMessage.StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider.GetRequiredService<TestAssistantLlmClient>();
            var request = provider.Requests.Last();
            Assert.Equal("What does SkyMate do?", request.Messages.Last().Content);
            Assert.Contains("Trip context is valid only for the current message", request.SystemInstruction, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CURRENT TRIP CONTEXT JSON FOR THIS MESSAGE (null", request.SystemInstruction, StringComparison.Ordinal);
            Assert.DoesNotContain("TripContextDestination", request.SystemInstruction, StringComparison.Ordinal);
            Assert.DoesNotContain("Museums", request.SystemInstruction, StringComparison.Ordinal);
        }

        await LoginAsync("trip-context-other@example.com");
        var otherConversation = await CreateConversationAsync("No other user's trip");
        var previousRequestCount = await GetProviderRequestCountAsync();
        var crossUserTrip = await _client.PostAsJsonAsync(
            $"/api/v1/conversations/{otherConversation.Id}/messages",
            new { content = "Show me that itinerary", tripId });
        Assert.Equal(HttpStatusCode.NotFound, crossUserTrip.StatusCode);
        Assert.Equal(previousRequestCount, await GetProviderRequestCountAsync());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null(typeof(AiConversation).GetProperty("TripId"));
        Assert.Null(typeof(AiConversationMessage).GetProperty("TripId"));
        Assert.Equal(4, await verifyContext.AiConversationMessages.CountAsync(message => message.ConversationId == ownerConversation.Id));
    }

    [Fact]
    public async Task AssistantEnforcesTenMessagesPerMinutePerAuthenticatedUser()
    {
        await RegisterAsync("rate-limit-one@example.com");
        await RegisterAsync("rate-limit-two@example.com");
        await LoginAsync("rate-limit-one@example.com");
        var firstConversation = await CreateConversationAsync("First user");

        HttpResponseMessage? last = null;
        for (var message = 0; message < 10; message++)
        {
            last = await _client.PostAsJsonAsync(
                $"/api/v1/conversations/{firstConversation.Id}/messages",
                new { content = $"Message {message}" });
            Assert.Equal(HttpStatusCode.Created, last.StatusCode);
        }

        last = await _client.PostAsJsonAsync(
            $"/api/v1/conversations/{firstConversation.Id}/messages",
            new { content = "Over the limit" });
        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
        Assert.Contains("assistant_rate_limited", await last.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await LoginAsync("rate-limit-two@example.com");
        var secondConversation = await CreateConversationAsync("Second user");
        Assert.Equal(HttpStatusCode.Created,
            (await _client.PostAsJsonAsync(
                $"/api/v1/conversations/{secondConversation.Id}/messages",
                new { content = "My own quota" })).StatusCode);
    }

    [Fact]
    public async Task ProviderFailureDoesNotPersistAnIncompleteTurn()
    {
        await RegisterAsync("provider-failure@example.com");
        await LoginAsync("provider-failure@example.com");
        var conversation = await CreateConversationAsync("No partial write");
        DateTime? previousUpdatedAt;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            previousUpdatedAt = (await context.AiConversations.SingleAsync(item => item.Id == conversation.Id)).UpdatedAt;
            scope.ServiceProvider.GetRequiredService<TestAssistantLlmClient>().FailRequests = true;
        }

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = "This request should fail." });
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await verifyContext.AiConversationMessages
            .Where(message => message.ConversationId == conversation.Id)
            .ToListAsync());
        Assert.Equal(previousUpdatedAt,
            (await verifyContext.AiConversations.SingleAsync(item => item.Id == conversation.Id)).UpdatedAt);
    }

    [Fact]
    public async Task BlankMessageReturnsBadRequestWithoutCallingProvider()
    {
        await RegisterAsync("blank-message@example.com");
        await LoginAsync("blank-message@example.com");
        var conversation = await CreateConversationAsync("Validation");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<TestAssistantLlmClient>();
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task MessageOverOneThousandCharactersReturnsTheDocumentedClientError()
    {
        await RegisterAsync("long-message@example.com");
        await LoginAsync("long-message@example.com");
        var conversation = await CreateConversationAsync("Length limit");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = new string('x', 1001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("message_too_long", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var scope = _factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<TestAssistantLlmClient>().Requests);
    }

    [Fact]
    public async Task KnowledgeBaseIsAdminOnlyEditableAndSeedDefaultsDoNotOverwriteRows()
    {
        const string email = "knowledge-admin@example.com";
        int adminId;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = "Knowledge",
                LastName = "Admin"
            };
            var created = await userManager.CreateAsync(admin, "SkyMatePass1");
            Assert.True(created.Succeeded, string.Join(";", created.Errors.Select(error => error.Code)));
            var assigned = await userManager.AddToRoleAsync(admin, "Admin");
            Assert.True(assigned.Succeeded, string.Join(";", assigned.Errors.Select(error => error.Code)));
            adminId = admin.Id;
        }

        await RegisterAsync("knowledge-user@example.com");
        await LoginAsync("knowledge-user@example.com");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _client.GetAsync("/api/v1/admin/assistant/knowledge-base")).StatusCode);

        await LoginAsync(email);
        var seed = await _client.PostAsJsonAsync("/api/v1/admin/assistant/knowledge-base/seed-defaults", new { });
        Assert.Equal(HttpStatusCode.OK, seed.StatusCode);
        Assert.Contains("\"added\":6", await seed.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var repeatSeed = await _client.PostAsJsonAsync("/api/v1/admin/assistant/knowledge-base/seed-defaults", new { });
        Assert.Equal(HttpStatusCode.OK, repeatSeed.StatusCode);
        Assert.Contains("\"added\":0", await repeatSeed.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var create = await _client.PostAsJsonAsync(
            "/api/v1/admin/assistant/knowledge-base",
            new { title = "Managed entry", content = "Initial text", isActive = true });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createdEntry = await create.Content.ReadFromJsonAsync<KnowledgeBaseEntrySummary>(JsonOptions);
        Assert.NotNull(createdEntry);
        var update = await _client.PutAsJsonAsync(
            $"/api/v1/admin/assistant/knowledge-base/{createdEntry.Id}",
            new { title = "Managed entry", content = "Updated without a code change", isActive = true });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Contains("Updated without a code change", await update.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.DeleteAsync($"/api/v1/admin/assistant/knowledge-base/{createdEntry.Id}")).StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(7, await context.KnowledgeBaseEntries.CountAsync());
        Assert.Equal(6, await context.KnowledgeBaseEntries.CountAsync(entry => entry.IsActive));
        Assert.False(await context.KnowledgeBaseEntries.AnyAsync(entry => entry.Id == createdEntry.Id && entry.IsActive));
        Assert.True(await context.Users.AnyAsync(user => user.Id == adminId));
    }

    [Fact]
    public async Task UsersCannotReadSendOrDeleteAnotherUsersConversation()
    {
        await RegisterAsync("owner@example.com");
        await RegisterAsync("other@example.com");
        await LoginAsync("owner@example.com");
        var conversation = await CreateConversationAsync("Private");

        await LoginAsync("other@example.com");
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/v1/conversations/{conversation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/v1/conversations/{conversation.Id}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PostAsJsonAsync(
                $"/api/v1/conversations/{conversation.Id}/messages",
                new { content = "Unauthorized" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.DeleteAsync($"/api/v1/conversations/{conversation.Id}")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await context.AiConversations.AnyAsync(item => item.Id == conversation.Id));
    }

    [Fact]
    public async Task UserCanReadAndPermanentlyDeleteConversationAndMessages()
    {
        await RegisterAsync("delete-conversation@example.com");
        await LoginAsync("delete-conversation@example.com");
        var conversation = await CreateConversationAsync("Delete me");
        Assert.Equal(HttpStatusCode.Created,
            (await _client.PostAsJsonAsync(
                $"/api/v1/conversations/{conversation.Id}/messages",
                new { content = "Save this pair." })).StatusCode);

        var messagesResponse = await _client.GetAsync($"/api/v1/conversations/{conversation.Id}/messages");
        Assert.Equal(HttpStatusCode.OK, messagesResponse.StatusCode);
        Assert.Equal(2, (await messagesResponse.Content.ReadFromJsonAsync<ConversationMessageResponse[]>(JsonOptions))!.Length);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.GetAsync($"/api/v1/conversations/{conversation.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.DeleteAsync($"/api/v1/conversations/{conversation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/v1/conversations/{conversation.Id}")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await context.AiConversations.AnyAsync(item => item.Id == conversation.Id));
        Assert.False(await context.AiConversationMessages.AnyAsync(item => item.ConversationId == conversation.Id));
    }

    [Fact]
    public async Task GeminiContinuationMetadataIsPersistedPrivatelyAndNeverReturnedByMessagesApi()
    {
        await RegisterAsync("provider-metadata@example.com");
        await LoginAsync("provider-metadata@example.com");
        var conversation = await CreateConversationAsync("Private provider metadata");
        const string providerMetadata = "[{\"type\":\"thought\",\"signature\":\"opaque-test-signature\"}]";
        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TestAssistantLlmClient>().ProviderContextJsonToReturn = providerMetadata;
        }

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { content = "Remember this turn." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("opaque-test-signature", responseBody, StringComparison.Ordinal);

        using var scopeAfterWrite = _factory.Services.CreateScope();
        var context = scopeAfterWrite.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(providerMetadata, await context.AiConversationMessages
            .Where(message => message.ConversationId == conversation.Id && message.Role == ConversationRole.Assistant)
            .Select(message => message.ProviderContextJson)
            .SingleAsync());

        var messagesResponse = await _client.GetAsync($"/api/v1/conversations/{conversation.Id}/messages");
        Assert.DoesNotContain("opaque-test-signature", await messagesResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConversationRemainsAvailableAfterTripEndsAndTripIsDeleted()
    {
        var userId = await RegisterAsync("trip-independent@example.com");
        await LoginAsync("trip-independent@example.com");
        var conversation = await CreateConversationAsync("No trip relationship");

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var trip = AddTrip(userId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10));
            context.Trips.Add(trip);
            await context.SaveChangesAsync();
            context.Trips.Remove(trip);
            await context.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK,
            (await _client.GetAsync($"/api/v1/conversations/{conversation.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await _client.PostAsJsonAsync(
                $"/api/v1/conversations/{conversation.Id}/messages",
                new { content = "Continue with no trip." })).StatusCode);
    }

    [Fact]
    public async Task InvalidConversationIdsReturnNotFoundAndUnauthenticatedRequestsReturnUnauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/conversations")).StatusCode);
        await RegisterAsync("invalid-id@example.com");
        await LoginAsync("invalid-id@example.com");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/conversations/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync("/api/v1/conversations/999999/messages")).StatusCode);
    }

    [Fact]
    public async Task AuthenticatedAdminWithoutUserRoleCannotUseConversationEndpoints()
    {
        const string email = "admin-only@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = "Test",
                LastName = "Admin"
            };
            var created = await userManager.CreateAsync(admin, "SkyMatePass1");
            Assert.True(created.Succeeded, string.Join(";", created.Errors.Select(error => error.Code)));
            var assigned = await userManager.AddToRoleAsync(admin, "Admin");
            Assert.True(assigned.Succeeded, string.Join(";", assigned.Errors.Select(error => error.Code)));
        }

        await LoginAsync(email);

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/conversations")).StatusCode);
    }

    private async Task<int> RegisterAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            firstName = "Sky",
            lastName = "Mate",
            email,
            password = "SkyMatePass1",
            confirmPassword = "SkyMatePass1"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterResponse>(JsonOptions))!.Id;
    }

    private async Task LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "SkyMatePass1"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions))!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
    }

    private async Task<ConversationResponse> CreateConversationAsync(string title)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/conversations", new { title });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ConversationResponse>(JsonOptions))!;
    }

    private async Task<int> GetProviderRequestCountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TestAssistantLlmClient>().Requests.Count;
    }

    private static Trip AddTrip(int userId, DateOnly startDate) => new()
    {
        UserId = userId,
        Destination = "Cairo",
        Budget = 1000,
        Currency = "USD",
        DaysNumber = 2,
        TravellersCount = 1,
        StartDate = startDate,
        Pace = Pace.Balanced
    };

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
