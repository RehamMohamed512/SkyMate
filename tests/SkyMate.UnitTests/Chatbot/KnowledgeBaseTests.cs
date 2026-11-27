using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SkyMate.Domain.Entities;
using SkyMate.Infrastructure.AI;
using SkyMate.Infrastructure.Services;
using SkyMate.UnitTests.Persistence;

namespace SkyMate.UnitTests.Chatbot;

/// <summary>Verifies approved defaults, insert-only seeding, active filtering, and cache invalidation.</summary>
public sealed class KnowledgeBaseTests
{
    [Fact]
    public async Task SeedsOnlySupportedDefaultsOnce()
    {
        var (connection, dbContext) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (dbContext)
        {
            var provider = new KnowledgeBaseProvider(dbContext, new MemoryCache(new MemoryCacheOptions()));
            var seeder = new KnowledgeBaseSeeder(dbContext);

            var first = await seeder.SeedDefaultsAsync();
            var second = await seeder.SeedDefaultsAsync();

            Assert.Equal(17, first.Added);
            Assert.Equal(0, first.AlreadyPresent);
            Assert.Equal(0, second.Added);
            Assert.Equal(17, second.AlreadyPresent);
            Assert.Equal(17, await dbContext.KnowledgeBaseEntries.CountAsync());
            Assert.Equal(17, await dbContext.KnowledgeBaseEntries.CountAsync(entry => entry.IsActive));
            Assert.Equal(0, await dbContext.KnowledgeBaseEntries.CountAsync(entry => !entry.IsActive));
            var titles = await dbContext.KnowledgeBaseEntries.Select(entry => entry.Title).ToListAsync();
            Assert.DoesNotContain(titles, title => title.Contains("Translation", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(titles, title => title.Contains("Airport", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(titles, title => title.Contains("Planner", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(17, (await provider.GetActiveAsync()).Count);
        }
    }

    [Fact]
    public async Task SeededAssistantContentDescribesTripRetentionAndDoesNotPromisePersonalDataFiltering()
    {
        var (connection, dbContext) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (dbContext)
        {
            await new KnowledgeBaseSeeder(dbContext).SeedDefaultsAsync();

            var assistant = await dbContext.KnowledgeBaseEntries
                .SingleAsync(entry => entry.Title == "Travel Assistant (chat)");
            Assert.Contains("associated with one of that user's trips", assistant.Content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("after the trip ends", assistant.Content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("can exist without a trip", assistant.Content, StringComparison.OrdinalIgnoreCase);

            var privacy = await dbContext.KnowledgeBaseEntries
                .SingleAsync(entry => entry.Title == "Data and AI usage");
            Assert.Contains("may be sent", privacy.Content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Avoid sharing sensitive personal information", privacy.Content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("It does not send your email address or phone number", privacy.Content, StringComparison.OrdinalIgnoreCase);

            var itinerary = await dbContext.KnowledgeBaseEntries
                .SingleAsync(entry => entry.Title == "Itineraries");
            Assert.Contains("planning estimates", itinerary.Content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("does not recalculate", itinerary.Content, StringComparison.OrdinalIgnoreCase);

            var unsupported = string.Join(" ", await dbContext.KnowledgeBaseEntries.Select(entry => entry.Content).ToListAsync());
            Assert.DoesNotContain("budget split", unsupported, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("exchange rates are updated daily", unsupported, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Translation Service", unsupported, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task SeederPreservesExistingAdministratorContentAndActivationStatus()
    {
        var (connection, dbContext) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (dbContext)
        {
            dbContext.KnowledgeBaseEntries.Add(new KnowledgeBaseEntry
            {
                Title = "What is SkyMate",
                Content = "Administrator-approved replacement text.",
                IsActive = false
            });
            await dbContext.SaveChangesAsync();

            var seeder = new KnowledgeBaseSeeder(dbContext);
            var result = await seeder.SeedDefaultsAsync();

            var existing = await dbContext.KnowledgeBaseEntries.SingleAsync(entry => entry.Title == "What is SkyMate");
            Assert.Equal(16, result.Added);
            Assert.Equal(1, result.AlreadyPresent);
            Assert.Equal("Administrator-approved replacement text.", existing.Content);
            Assert.False(existing.IsActive);
            Assert.Equal(17, await dbContext.KnowledgeBaseEntries.CountAsync());
        }
    }

    [Fact]
    public async Task SeederDoesNotDuplicateASeedEntryAfterAdministratorRenamesIt()
    {
        var (connection, dbContext) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (dbContext)
        {
            var seeder = new KnowledgeBaseSeeder(dbContext);
            await seeder.SeedDefaultsAsync();
            var entry = await dbContext.KnowledgeBaseEntries.SingleAsync(item => item.Title == "What is SkyMate");
            entry.Title = "SkyMate overview, approved by admin";
            entry.Content = "Administrator edited content remains authoritative.";
            await dbContext.SaveChangesAsync();

            var result = await seeder.SeedDefaultsAsync();

            Assert.Equal(0, result.Added);
            Assert.Equal(17, result.AlreadyPresent);
            Assert.Equal(17, await dbContext.KnowledgeBaseEntries.CountAsync());
            Assert.Equal("Administrator edited content remains authoritative.", entry.Content);
            Assert.Equal("SkyMate overview, approved by admin", entry.Title);
        }
    }

    [Fact]
    public async Task ActiveEntriesAreCachedAndAdministratorChangesInvalidateTheCache()
    {
        var (connection, dbContext) = await SqliteTestContext.CreateAsync();
        await using (connection)
        await using (dbContext)
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var provider = new KnowledgeBaseProvider(dbContext, cache);
            var service = new KnowledgeBaseService(dbContext, provider, new KnowledgeBaseSeeder(dbContext));
            await service.SeedDefaultsAsync();

            Assert.Equal(17, (await service.GetActiveAsync()).Count);
            var entry = await dbContext.KnowledgeBaseEntries.SingleAsync(item => item.Title == "What is SkyMate");
            Assert.True(await service.DeactivateAsync(entry.Id));
            Assert.Equal(16, (await service.GetActiveAsync()).Count);
            Assert.Equal(17, (await service.ListAsync()).Count);
        }
    }
}
