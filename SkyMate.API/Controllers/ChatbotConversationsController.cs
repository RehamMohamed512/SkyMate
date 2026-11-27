using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SkyMate.API.DTOs.Chatbot;
using SkyMate.API.Security;
using SkyMate.Application.DTOs.Chatbot;
using SkyMate.Application.Interfaces.Chatbot;

namespace SkyMate.API.Controllers;

/// <summary>Provides user-owned conversation and message endpoints.</summary>
[ApiController]
[Route("api/v1/conversations")]
[Authorize(Roles = "User")]
public sealed class ChatbotConversationsController(IChatbotConversationService conversationService) : ControllerBase
{
    private const int MaxMessageLength = 1000;
    private const string AssistantMessageRateLimitPolicy = "assistant-message";
    /// <summary>Lists the authenticated user's conversations, newest activity first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var conversations = await conversationService.ListForUserAsync(userId.Value, cancellationToken);
        return Ok(conversations.Select(ToResponse).ToArray());
    }

    /// <summary>Creates a conversation associated with an owned trip.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        CreateConversationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var conversation = await conversationService.CreateForUserAsync(
            userId.Value,
            request.TripId,
            request.Title,
            cancellationToken);
        if (conversation is null)
        {
            return NotFound();
        }
        return CreatedAtAction(nameof(Get), new { id = conversation.Id }, ToResponse(conversation));
    }

    /// <summary>Gets conversation details when they belong to the authenticated user.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var conversation = await conversationService.GetForUserAsync(id, userId.Value, cancellationToken);
        return conversation is null ? NotFound() : Ok(ToResponse(conversation));
    }

    /// <summary>Lists messages when the conversation belongs to the authenticated user.</summary>
    [HttpGet("{id:int}/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMessages(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var messages = await conversationService.ListMessagesForUserAsync(id, userId.Value, cancellationToken);
        return messages is null
            ? NotFound()
            : Ok(messages.Select(ToResponse).ToArray());
    }

    /// <summary>Generates and persists a user/assistant message pair.</summary>
    [HttpPost("{id:int}/messages")]
    [EnableRateLimiting(AssistantMessageRateLimitPolicy)]
    [ProducesResponseType(typeof(ConversationTurnResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> SendMessage(
        int id,
        AddConversationMessageRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest(new { code = "message_required" });
        }

        if (content.Length > MaxMessageLength)
        {
            return BadRequest(new { code = "message_too_long", max = MaxMessageLength });
        }

        var turn = await conversationService.SendMessageAsync(
            id,
            userId.Value,
            content,
            cancellationToken);
        if (turn is null)
        {
            return NotFound();
        }

        return CreatedAtAction(
            nameof(ListMessages),
            new { id },
            new ConversationTurnResponse(ToResponse(turn.UserMessage), ToResponse(turn.AssistantMessage)));
    }

    /// <summary>Permanently deletes an owned conversation and all its messages.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var deleted = await conversationService.DeleteForUserAsync(id, userId.Value, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private static ConversationResponse ToResponse(ConversationSummary conversation) => new(
        conversation.Id,
        conversation.TripId,
        conversation.Title,
        conversation.CreatedAt,
        conversation.UpdatedAt);

    private static ConversationMessageResponse ToResponse(ConversationMessageSummary message) => new(
        message.Id,
        message.Role,
        message.Content,
        message.CreatedAt);
}
