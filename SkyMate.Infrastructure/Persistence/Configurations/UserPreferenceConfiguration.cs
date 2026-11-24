using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyMate.Domain.Entities;

namespace SkyMate.Infrastructure.Persistence.Configurations;

/// <summary>Configures user preference fields and the unique preference index.</summary>
public sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    /// <summary>Maps preference fields and prevents duplicate preferences for a user.</summary>
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.ToTable("UserPreferences");
        builder.HasKey(preference => preference.Id);
        builder.Property(preference => preference.Type).HasMaxLength(50).IsRequired();
        builder.Property(preference => preference.Interest).HasMaxLength(100).IsRequired();
        builder.Property(preference => preference.Pace).HasMaxLength(20);
        builder.Property(preference => preference.CreatedAt).HasColumnType("datetime2");
        builder.HasIndex(preference => new { preference.UserId, preference.Type, preference.Interest }).IsUnique();
    }
}
