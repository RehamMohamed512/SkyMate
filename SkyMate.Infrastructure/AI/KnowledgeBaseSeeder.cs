using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkyMate.Application.DTOs.Chatbot;
using SkyMate.Domain.Entities;
using SkyMate.Infrastructure.Persistence;

namespace SkyMate.Infrastructure.AI;

/// <summary>Idempotently inserts missing AI-team defaults without editing existing managed entries.</summary>
public sealed class KnowledgeBaseSeeder(AppDbContext dbContext)
{
    private const string ResourceSuffix = ".AI.knowledge_base_seed.json";

    public async Task<KnowledgeBaseSeedResult> SeedDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var items = await ReadDefaultsAsync(cancellationToken);
        var existingEntries = await dbContext.KnowledgeBaseEntries.ToListAsync(cancellationToken);
        var entriesBySeedKey = existingEntries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.SeedKey))
            .ToDictionary(entry => entry.SeedKey!, StringComparer.OrdinalIgnoreCase);
        var entriesByTitle = existingEntries
            .GroupBy(entry => entry.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var missingItems = new List<KnowledgeBaseEntry>();
        var adoptedEntries = 0;

        foreach (var item in items)
        {
            var title = item.Title.Trim();
            var seedKey = CreateSeedKey(title);
            if (entriesBySeedKey.ContainsKey(seedKey))
            {
                continue;
            }

            if (entriesByTitle.TryGetValue(title, out var existingEntry))
            {
                // Adopt a pre-existing same-title row without changing its admin-owned content/status.
                if (existingEntry.SeedKey is null)
                {
                    existingEntry.SeedKey = seedKey;
                    entriesBySeedKey.Add(seedKey, existingEntry);
                }

                adoptedEntries++;
                continue;
            }

            var newEntry = new KnowledgeBaseEntry
            {
                SeedKey = seedKey,
                Title = title,
                Content = item.Content,
                IsActive = item.IsActive
            };
            missingItems.Add(newEntry);
            entriesByTitle.Add(title, newEntry);
            entriesBySeedKey.Add(seedKey, newEntry);
        }

        if (missingItems.Count > 0 || adoptedEntries > 0)
        {
            if (missingItems.Count > 0)
            {
                dbContext.KnowledgeBaseEntries.AddRange(missingItems);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new KnowledgeBaseSeedResult(missingItems.Count, items.Count - missingItems.Count);
    }

    private static string CreateSeedKey(string title) => $"seed:{title.ToLowerInvariant()}";

    private static async Task<IReadOnlyList<SeedItem>> ReadDefaultsAsync(CancellationToken cancellationToken)
    {
        var assembly = typeof(KnowledgeBaseSeeder).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The AI-team knowledge-base resource is missing.");

        var items = await JsonSerializer.DeserializeAsync<List<SeedItem>>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        if (items is null || items.Count == 0)
        {
            throw new InvalidOperationException("The knowledge-base defaults resource is empty or invalid.");
        }

        if (items.Any(item => string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Content)) ||
            items.Select(item => item.Title.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count)
        {
            throw new InvalidOperationException("The AI-team knowledge-base defaults contain an invalid or duplicate entry.");
        }

        return items;
    }

    private sealed record SeedItem(string Title, string Content, bool IsActive);
}
