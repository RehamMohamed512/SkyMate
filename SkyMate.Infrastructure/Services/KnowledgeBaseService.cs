using Microsoft.EntityFrameworkCore;
using SkyMate.Application.AI;
using SkyMate.Application.DTOs.Chatbot;
using SkyMate.Application.Interfaces.Chatbot;
using SkyMate.Domain.Entities;
using SkyMate.Infrastructure.AI;
using SkyMate.Infrastructure.Persistence;

namespace SkyMate.Infrastructure.Services;

/// <summary>Provides cached active knowledge and guarded administrator operations.</summary>
public sealed class KnowledgeBaseService(
    AppDbContext dbContext,
    KnowledgeBaseProvider provider,
    KnowledgeBaseSeeder seeder) : IKnowledgeBaseService
{
    public Task<IReadOnlyList<AssistantKnowledgeBaseItem>> GetActiveAsync(
        CancellationToken cancellationToken = default) => provider.GetActiveAsync(cancellationToken);

    public async Task<IReadOnlyList<KnowledgeBaseEntrySummary>> ListAsync(
        CancellationToken cancellationToken = default) => await dbContext.KnowledgeBaseEntries
        .AsNoTracking()
        .OrderBy(entry => entry.Id)
        .Select(entry => new KnowledgeBaseEntrySummary(entry.Id, entry.Title, entry.Content, entry.IsActive))
        .ToListAsync(cancellationToken);

    public Task<KnowledgeBaseEntrySummary?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeBaseEntries
            .AsNoTracking()
            .Where(entry => entry.Id == id)
            .Select(entry => new KnowledgeBaseEntrySummary(entry.Id, entry.Title, entry.Content, entry.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<KnowledgeBaseEntrySummary> CreateAsync(
        KnowledgeBaseEntryUpdate entry,
        CancellationToken cancellationToken = default)
    {
        var title = entry.Title.Trim();
        if (await HasTitleAsync(title, null, cancellationToken))
        {
            throw new KnowledgeBaseTitleConflictException();
        }

        var entity = new KnowledgeBaseEntry
        {
            Title = title,
            Content = entry.Content.Trim(),
            IsActive = entry.IsActive
        };
        dbContext.KnowledgeBaseEntries.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        provider.Invalidate();
        return ToSummary(entity);
    }

    public async Task<KnowledgeBaseEntrySummary?> UpdateAsync(
        int id,
        KnowledgeBaseEntryUpdate entry,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.KnowledgeBaseEntries
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var title = entry.Title.Trim();
        if (await HasTitleAsync(title, id, cancellationToken))
        {
            throw new KnowledgeBaseTitleConflictException();
        }

        entity.Title = title;
        entity.Content = entry.Content.Trim();
        entity.IsActive = entry.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        provider.Invalidate();
        return ToSummary(entity);
    }

    public async Task<bool> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.KnowledgeBaseEntries
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        provider.Invalidate();
        return true;
    }

    public async Task<KnowledgeBaseSeedResult> SeedDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var result = await seeder.SeedDefaultsAsync(cancellationToken);
        if (result.Added > 0)
        {
            provider.Invalidate();
        }

        return result;
    }

    private Task<bool> HasTitleAsync(string title, int? exceptId, CancellationToken cancellationToken)
    {
        var normalized = title.ToUpper();
        var query = dbContext.KnowledgeBaseEntries.Where(entry => entry.Title.ToUpper() == normalized);
        if (exceptId.HasValue)
        {
            query = query.Where(entry => entry.Id != exceptId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    private static KnowledgeBaseEntrySummary ToSummary(KnowledgeBaseEntry entry) =>
        new(entry.Id, entry.Title, entry.Content, entry.IsActive);
}

