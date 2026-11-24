using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkyMate.API.DTOs.Chatbot;
using SkyMate.Application.AI;
using SkyMate.Application.DTOs.Chatbot;
using SkyMate.Application.Interfaces.Chatbot;

namespace SkyMate.API.Controllers;

/// <summary>Lets authorized administrators seed and manage assistant knowledge without a redeploy.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/assistant/knowledge-base")]
public sealed class KnowledgeBaseAdminController(IKnowledgeBaseService knowledgeBaseService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<KnowledgeBaseEntrySummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await knowledgeBaseService.ListAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(KnowledgeBaseEntrySummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var entry = await knowledgeBaseService.GetAsync(id, cancellationToken);
        return entry is null ? NotFound() : Ok(entry);
    }

    [HttpPost]
    [ProducesResponseType(typeof(KnowledgeBaseEntrySummary), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        KnowledgeBaseEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryCreateUpdate(request, out var entry))
        {
            return BadRequest(new { code = "invalid_entry" });
        }

        try
        {
            var created = await knowledgeBaseService.CreateAsync(entry, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (KnowledgeBaseTitleConflictException)
        {
            return Conflict(new { code = "title_exists" });
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(KnowledgeBaseEntrySummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        KnowledgeBaseEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryCreateUpdate(request, out var entry))
        {
            return BadRequest(new { code = "invalid_entry" });
        }

        try
        {
            var updated = await knowledgeBaseService.UpdateAsync(id, entry, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (KnowledgeBaseTitleConflictException)
        {
            return Conflict(new { code = "title_exists" });
        }
    }

    /// <summary>Deactivates an entry without deleting it so an administrator can restore it later.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken) =>
        await knowledgeBaseService.DeactivateAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Inserts the original AI-team entries that are missing, preserving every existing row.</summary>
    [HttpPost("seed-defaults")]
    [ProducesResponseType(typeof(KnowledgeBaseSeedResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedDefaults(CancellationToken cancellationToken) =>
        Ok(await knowledgeBaseService.SeedDefaultsAsync(cancellationToken));

    private static bool TryCreateUpdate(
        KnowledgeBaseEntryRequest? request,
        out KnowledgeBaseEntryUpdate entry)
    {
        var title = request?.Title?.Trim();
        var content = request?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200 ||
            string.IsNullOrWhiteSpace(content) || content.Length > 4000)
        {
            entry = null!;
            return false;
        }

        entry = new KnowledgeBaseEntryUpdate(title, content, request!.IsActive);
        return true;
    }
}
