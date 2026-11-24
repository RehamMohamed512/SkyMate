using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyMate.Domain.Entities;

namespace SkyMate.Infrastructure.Persistence.Configurations;

/// <summary>
/// <summary>Configures activity columns, database constraints, and indexes.</summary>
/// </summary>
public sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    /// <summary>Maps activity properties and applies its data rules.</summary>
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("Activities", table =>
        {
            // Database constraints protect data even when a write bypasses API validation.
            table.HasCheckConstraint("CK_Activities_EstimatedCost_NonNegative", "[EstimatedCost] >= 0");
            table.HasCheckConstraint("CK_Activities_ActivityOrder_Minimum", "[ActivityOrder] >= 1");
            table.HasCheckConstraint("CK_Activities_Time_Order", "[StartTime] IS NULL OR [EndTime] IS NULL OR [EndTime] > [StartTime]");
            table.HasCheckConstraint("CK_Activities_ActivityType_Values", "[ActivityType] IN ('Sightseeing', 'Food', 'Shopping', 'Nature', 'Culture', 'Adventure', 'Transport', 'Accommodation', 'Other')");
            table.HasCheckConstraint("CK_Activities_CostBasis_Values", "[CostBasis] IN ('PerPerson', 'PerGroup', 'Free')");
        });
        builder.HasKey(activity => activity.Id);
        builder.Property(activity => activity.Name).HasMaxLength(200).IsRequired();
        builder.Property(activity => activity.Description).HasMaxLength(1000);
        builder.Property(activity => activity.LocationName).HasMaxLength(200);
        builder.Property(activity => activity.ActivityType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(activity => activity.EstimatedCost).HasPrecision(18, 2);
        builder.Property(activity => activity.CostBasis).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(activity => activity.StartTime).HasColumnType("time");
        builder.Property(activity => activity.EndTime).HasColumnType("time");
        builder.Property(activity => activity.CreatedAt).HasColumnType("datetime2");
        // An order number is unique within its day; the same number may appear on another day.
        builder.HasIndex(activity => new { activity.DayId, activity.ActivityOrder }).IsUnique();
    }
}
