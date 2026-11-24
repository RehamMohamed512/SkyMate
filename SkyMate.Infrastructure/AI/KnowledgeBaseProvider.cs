using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SkyMate.Application.AI;
using SkyMate.Infrastructure.Persistence;

namespace SkyMate.Infrastructure.AI;

/// <summary>Reads active knowledge from SQL Server and caches the small prompt corpus for five minutes.</summary>
public sealed class KnowledgeBaseProvider(AppDbContext dbContext, IMemoryCache cache)
{
    private const string CacheKey = "assistant:knowledge-base:active";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyList<AssistantKnowledgeBaseItem>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await dbContext.KnowledgeBaseEntries
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.Id)
                .Select(item => new AssistantKnowledgeBaseItem(item.Title, item.Content))
                .ToListAsync(cancellationToken);
        });

        return items ?? [];
    }

    /// <summary>Clears this node's cached copy after an administrator changes entries.</summary>
    public void Invalidate() => cache.Remove(CacheKey);
}
