using SkyMate.Application.AI;
using SkyMate.Application.DTOs.Chatbot;

namespace SkyMate.Application.Interfaces.Chatbot;

/// <summary>Loads active assistant knowledge and manages defaults and edits for administrators.</summary>
public interface IKnowledgeBaseService
{
    Task<IReadOnlyList<AssistantKnowledgeBaseItem>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeBaseEntrySummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<KnowledgeBaseEntrySummary?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<KnowledgeBaseEntrySummary> CreateAsync(
        KnowledgeBaseEntryUpdate entry,
        CancellationToken cancellationToken = default);

    Task<KnowledgeBaseEntrySummary?> UpdateAsync(
        int id,
        KnowledgeBaseEntryUpdate entry,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(int id, CancellationToken cancellationToken = default);

    Task<KnowledgeBaseSeedResult> SeedDefaultsAsync(CancellationToken cancellationToken = default);
}
