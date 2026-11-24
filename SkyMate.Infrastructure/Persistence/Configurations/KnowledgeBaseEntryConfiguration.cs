using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyMate.Domain.Entities;

namespace SkyMate.Infrastructure.Persistence.Configurations;

/// <summary>Maps the editable assistant knowledge table and its lookup indexes.</summary>
public sealed class KnowledgeBaseEntryConfiguration : IEntityTypeConfiguration<KnowledgeBaseEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<KnowledgeBaseEntry> builder)
    {
        builder.ToTable("KnowledgeBaseEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.SeedKey).HasMaxLength(205);
        builder.Property(entry => entry.Title).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.Content).HasMaxLength(4000).IsRequired();
        builder.Property(entry => entry.IsActive).HasDefaultValue(true).IsRequired();
        builder.HasIndex(entry => entry.Title).IsUnique();
        builder.HasIndex(entry => entry.SeedKey)
            .IsUnique()
            .HasFilter("[SeedKey] IS NOT NULL");
        builder.HasIndex(entry => new { entry.IsActive, entry.Id });
    }
}
