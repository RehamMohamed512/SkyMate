using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyMate.Domain.Entities;

namespace SkyMate.Infrastructure.Persistence.Configurations;

/// <summary>Configures trip columns, validation constraints, and owner/date indexes.</summary>
public sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    /// <summary>Maps trip fields and applies database constraints and indexes.</summary>
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("Trips", table =>
        {
            // Check constraints enforce domain rules at the database level as well.
            table.HasCheckConstraint("CK_Trips_Budget_NonNegative", "[Budget] >= 0");
            table.HasCheckConstraint("CK_Trips_DaysNumber_Range", "[DaysNumber] BETWEEN 1 AND 30");
            table.HasCheckConstraint("CK_Trips_TravellersCount_Minimum", "[TravellersCount] >= 1");
            table.HasCheckConstraint("CK_Trips_Currency_Length", "LEN([Currency]) = 3");
            table.HasCheckConstraint("CK_Trips_Pace_Values", "[Pace] IN ('Relaxed', 'Balanced', 'Packed')");
        });
        builder.HasKey(trip => trip.Id);
        // Supports a composite conversation FK that guarantees the trip and conversation owner match.
        builder.HasAlternateKey(trip => new { trip.Id, trip.UserId });
        builder.Property(trip => trip.Destination).HasMaxLength(200).IsRequired();
        builder.Property(trip => trip.Budget).HasPrecision(18, 2);
        builder.Property(trip => trip.Currency).HasColumnType("char(3)").HasMaxLength(3).HasDefaultValue("USD").IsRequired();
        builder.Property(trip => trip.StartDate).HasColumnType("date");
        builder.Property(trip => trip.Pace).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(trip => trip.CreatedAt).HasColumnType("datetime2");
        builder.Property(trip => trip.UpdatedAt).HasColumnType("datetime2");
        builder.HasIndex(trip => new { trip.UserId, trip.StartDate });
    }
}
