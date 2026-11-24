using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyMate.Domain.Entities;

namespace SkyMate.Infrastructure.Persistence.Configurations;

/// <summary>Configures chatbot message columns, allowed roles, and message ordering.</summary>
public sealed class AiConversationMessageConfiguration : IEntityTypeConfiguration<AiConversationMessage>
{
    /// <summary>Maps message fields and applies role and query constraints.</summary>
    public void Configure(EntityTypeBuilder<AiConversationMessage> builder)
    {
        // This constraint rejects undefined role values from any write path.
        builder.ToTable("AiConversationMessages", table => table.HasCheckConstraint(
            "CK_AiConversationMessages_Role_Values",
            "[Role] IN ('User', 'Assistant')"));
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(message => message.Content).IsRequired();
        // Leave the provider's string mapping intact: SQL Server maps an unbounded string
        // to nvarchar(max), while SQLite needs its native TEXT affinity for the test model.
        builder.Property(message => message.ProviderContextJson);
        builder.Property(message => message.CreatedAt).HasColumnType("datetime2");
        builder.HasIndex(message => new { message.ConversationId, message.CreatedAt });
    }
}
