using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyMate.Domain.Entities;
using SkyMate.Infrastructure.Identity;

namespace SkyMate.Infrastructure.Persistence.Configurations;

/// <summary>Configures extra profile fields, indexes, and relationships for Identity users.</summary>
public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    /// <summary>Maps user profile fields and configures related data.</summary>
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("AspNetUsers");
        builder.Property(user => user.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(user => user.LastName).HasMaxLength(100).IsRequired();
        builder.Property(user => user.CreatedAt).HasColumnType("datetime2");
        builder.Property(user => user.UpdatedAt).HasColumnType("datetime2");
        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("EmailIndex")
            .HasFilter("[NormalizedEmail] IS NOT NULL");

        builder.HasMany(user => user.Preferences)
            .WithOne()
            .HasForeignKey(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(user => user.Trips)
            .WithOne()
            .HasForeignKey(trip => trip.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(user => user.AiConversations)
            .WithOne()
            .HasForeignKey(conversation => conversation.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
