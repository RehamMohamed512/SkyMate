using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyMate.Domain.Entities;

namespace SkyMate.Infrastructure.Persistence.Configurations;

/// <summary>Configures the many-to-many link between trips and interests.</summary>
public sealed class TripInterestConfiguration : IEntityTypeConfiguration<TripInterest>
{
    /// <summary>Configures the composite key, relationships, and delete behavior.</summary>
    public void Configure(EntityTypeBuilder<TripInterest> builder)
    {
        builder.ToTable("TripInterests");
        builder.HasKey(link => new { link.TripId, link.InterestId });
        builder.HasOne(link => link.Trip)
            .WithMany(trip => trip.TripInterests)
            .HasForeignKey(link => link.TripId)
            .OnDelete(DeleteBehavior.Cascade);
        // Deleting one trip must not delete an interest shared by other trips.
        builder.HasOne(link => link.Interest)
            .WithMany(interest => interest.TripInterests)
            .HasForeignKey(link => link.InterestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(link => link.InterestId);
    }
}
