using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkyMate.Domain.Entities;

namespace SkyMate.Infrastructure.Persistence.Configurations;

/// <summary>Configures interest names and the unique lookup index.</summary>
public sealed class InterestConfiguration : IEntityTypeConfiguration<Interest>
{
    /// <summary>Maps the interest name and enforces its database constraints.</summary>
    public void Configure(EntityTypeBuilder<Interest> builder)
    {
        // The database requires a trimmed, non-empty interest name.
        builder.ToTable("Interests", table => table.HasCheckConstraint(
            "CK_Interests_Name_Trimmed",
            "LEN([Name]) > 0 AND DATALENGTH([Name]) = DATALENGTH(LTRIM(RTRIM([Name])))"));
        builder.HasKey(interest => interest.Id);
        builder.Property(interest => interest.Name)
            .HasMaxLength(100)
            .UseCollation("Latin1_General_100_CI_AS")
            .IsRequired();
        builder.HasIndex(interest => interest.Name).IsUnique();
    }
}
