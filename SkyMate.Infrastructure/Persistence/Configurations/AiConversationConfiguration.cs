using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyMate.Domain.Entities;

namespace SkyMate.Infrastructure.Persistence.Configurations;

/// <summary>Configures conversation ownership, messages, indexes, and delete behavior.</summary>
public sealed class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    /// <summary>Maps conversation properties and configures its relationships and indexes.</summary>
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        builder.ToTable("AiConversations");
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.Title).HasMaxLength(200);
        builder.Property(conversation => conversation.CreatedAt).HasColumnType("datetime2");
        builder.Property(conversation => conversation.UpdatedAt).HasColumnType("datetime2");
        builder.HasIndex(conversation => new { conversation.UserId, conversation.UpdatedAt });
        builder.HasIndex(conversation => new { conversation.TripId, conversation.UserId, conversation.CreatedAt });
        // Keep existing conversations whose former TripId was removed. New conversations are
        // created with a trip, and the composite FK prevents cross-user trip associations.
        // Restrict physical trip deletion so it cannot erase retained conversations.
        builder.HasOne(conversation => conversation.Trip)
            .WithMany(trip => trip.Conversations)
            .HasForeignKey(conversation => new { conversation.TripId, conversation.UserId })
            .HasPrincipalKey(trip => new { trip.Id, trip.UserId })
            .OnDelete(DeleteBehavior.ClientNoAction);
        // Deleting a conversation deletes its messages, but deleting a message keeps the conversation.
        builder.HasMany(conversation => conversation.Messages)
            .WithOne(message => message.Conversation)
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
