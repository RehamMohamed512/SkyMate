using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SkyMate.Application.AI;
using SkyMate.Application.DTOs.Chatbot;
using SkyMate.Application.Interfaces.Chatbot;
using SkyMate.Domain.Entities;
using SkyMate.Domain.Enums;
using SkyMate.Infrastructure.AI;
using SkyMate.Infrastructure.Persistence;

namespace SkyMate.Infrastructure.Services;

/// <summary>Persists conversations owned by users and delegates text generation to the assistant service.</summary>
public sealed class ChatbotConversationService(
    AppDbContext dbContext,
    IAssistantService assistantService,
    IOptions<AssistantAiOptions> options,
    TimeProvider timeProvider) : IChatbotConversationService
{
    private readonly int _maxHistoryMessages = options.Value.MaxHistoryMessages;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConversationSummary>> ListForUserAsync(
        int userId,
        CancellationToken cancellationToken = default) => await dbContext.AiConversations
        .AsNoTracking()
        .Where(conversation => conversation.UserId == userId)
        .OrderByDescending(conversation => conversation.UpdatedAt)
        .ThenByDescending(conversation => conversation.Id)
        .Select(conversation => new ConversationSummary(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt))
        .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<ConversationSummary> CreateForUserAsync(
        int userId,
        string? title,
        CancellationToken cancellationToken = default)
    {
        var now = UtcNow();
        var conversation = new AiConversation
        {
            UserId = userId,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.AiConversations.Add(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToSummary(conversation);
    }

    /// <inheritdoc />
    public async Task<ConversationSummary?> GetForUserAsync(
        int conversationId,
        int userId,
        CancellationToken cancellationToken = default) => await dbContext.AiConversations
        .AsNoTracking()
        .Where(conversation => conversation.Id == conversationId && conversation.UserId == userId)
        .Select(conversation => new ConversationSummary(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt))
        .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> DeleteForUserAsync(
        int conversationId,
        int userId,
        CancellationToken cancellationToken = default) => await dbContext.AiConversations
        .Where(conversation => conversation.Id == conversationId && conversation.UserId == userId)
        .ExecuteDeleteAsync(cancellationToken) > 0;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConversationMessageSummary>?> ListMessagesForUserAsync(
        int conversationId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var ownsConversation = await dbContext.AiConversations
            .AsNoTracking()
            .AnyAsync(conversation => conversation.Id == conversationId && conversation.UserId == userId,
                cancellationToken);
        if (!ownsConversation)
        {
            return null;
        }

        return await dbContext.AiConversationMessages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .Select(message => new ConversationMessageSummary(
                message.Id,
                message.Role,
                message.Content,
                message.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConversationTurnSummary?> SendMessageAsync(
        int conversationId,
        int userId,
        string content,
        int? tripId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await dbContext.AiConversations
            .SingleOrDefaultAsync(item => item.Id == conversationId && item.UserId == userId,
                cancellationToken);
        if (conversation is null)
        {
            return null;
        }

        AssistantTripContext? tripContext = null;
        if (tripId.HasValue)
        {
            var trip = await dbContext.Trips
                .AsNoTracking()
                .Where(item => item.Id == tripId.Value && item.UserId == userId)
                .AsSplitQuery()
                .Include(item => item.TripInterests)
                    .ThenInclude(link => link.Interest)
                .Include(item => item.Days.OrderBy(day => day.DayNumber))
                    .ThenInclude(day => day.Activities.OrderBy(activity => activity.ActivityOrder))
                .SingleOrDefaultAsync(cancellationToken);
            if (trip is null)
            {
                return null;
            }

            // This context belongs only to this request. TripId is not persisted on the conversation or messages.
            tripContext = AssistantTripContextMapper.Map(trip);
        }

        var storedHistory = await dbContext.AiConversationMessages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(_maxHistoryMessages)
            .ToListAsync(cancellationToken);
        storedHistory.Reverse();

        // Generate before changing tracked state so provider failures do not leave a user-only turn.
        var history = storedHistory
            .Select(message => new AssistantLlmTurn(message.Role, message.Content, message.ProviderContextJson))
            .ToArray();
        var assistantResponse = await assistantService.GenerateReplyAsync(
            content,
            history,
            tripContext,
            cancellationToken);

        // The conversation may have been removed while the provider request was in flight.
        var stillOwned = await dbContext.AiConversations
            .AsNoTracking()
            .AnyAsync(item => item.Id == conversationId && item.UserId == userId, cancellationToken);
        if (!stillOwned)
        {
            return null;
        }

        var createdAt = UtcNow();
        conversation.UpdatedAt = createdAt;
        var userMessage = new AiConversationMessage
        {
            ConversationId = conversationId,
            Role = ConversationRole.User,
            Content = content,
            CreatedAt = createdAt
        };
        var assistantMessage = new AiConversationMessage
        {
            ConversationId = conversationId,
            Role = ConversationRole.Assistant,
            Content = assistantResponse.Text,
            ProviderContextJson = assistantResponse.ProviderContextJson,
            CreatedAt = createdAt
        };

        dbContext.AiConversationMessages.AddRange(userMessage, assistantMessage);
        // One SaveChanges call keeps the message pair and updated timestamp in one transaction.
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ConversationTurnSummary(
            ToSummary(userMessage),
            ToSummary(assistantMessage));
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static ConversationSummary ToSummary(AiConversation conversation) => new(
        conversation.Id,
        conversation.Title,
        conversation.CreatedAt,
        conversation.UpdatedAt);

    private static ConversationMessageSummary ToSummary(AiConversationMessage message) => new(
        message.Id,
        message.Role,
        message.Content,
        message.CreatedAt);
}
