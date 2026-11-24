using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyMate.Domain.Entities;

namespace SkyMate.Infrastructure.Persistence.Configurations;

/// <summary>Configures trip days, their order, and their activity relationship.</summary>
public sealed class TripDayConfiguration : IEntityTypeConfiguration<TripDay>
{
    /// <summary>Maps day fields and configures ordering and activity deletion.</summary>
    public void Configure(EntityTypeBuilder<TripDay> builder)
    {
        // Day numbers must start at 1 and be unique within a trip.
        builder.ToTable("TripDays", table => table.HasCheckConstraint("CK_TripDays_DayNumber_Minimum", "[DayNumber] >= 1"));
        builder.HasKey(day => day.Id);
        builder.Property(day => day.Date).HasColumnType("date");
        builder.Property(day => day.WeekDay).HasMaxLength(20).IsRequired();
        builder.Property(day => day.CreatedAt).HasColumnType("datetime2");
        builder.HasIndex(day => new { day.TripId, day.DayNumber }).IsUnique();
        builder.HasIndex(day => new { day.TripId, day.Date }).IsUnique();
        builder.HasMany(day => day.Activities)
            .WithOne(activity => activity.Day)
            .HasForeignKey(activity => activity.DayId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
