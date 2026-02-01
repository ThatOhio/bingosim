using BingoSim.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BingoSim.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Event entity. Rows stored as JSON string column.
/// Event entity uses _rowsJson backing field; Rows property deserializes on access.
/// </summary>
public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Duration)
            .IsRequired();

        builder.Property(e => e.UnlockPointsRequiredPerRow)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.HasIndex(e => e.CreatedAt)
            .IsDescending();

        // Rows as JSON string - Event._rowsJson; no ValueConverter so EF does not discover Row/Tile/ActivityModifierRule
        builder.Property<string>("_rowsJson")
            .HasColumnName("Rows")
            .HasColumnType("jsonb");
    }
}
