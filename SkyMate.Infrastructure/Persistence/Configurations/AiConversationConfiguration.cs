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
        // Deleting a conversation deletes its messages, but deleting a message keeps the conversation.
        builder.HasMany(conversation => conversation.Messages)
            .WithOne(message => message.Conversation)
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
