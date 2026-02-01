using BingoSim.Core.Entities;
using BingoSim.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BingoSim.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Event entity. Rows (with Tiles and TileActivityRules) stored as JSON.
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

        // Rows as JSON (nested: Row -> Tiles -> TileActivityRules with Requirements, Modifiers)
        builder.Navigation(e => e.Rows).HasField("_rows");
        builder.OwnsMany(e => e.Rows, rowBuilder =>
        {
            rowBuilder.ToJson("Rows");
            rowBuilder.Property(r => r.Index);

            rowBuilder.OwnsMany(r => r.Tiles, tileBuilder =>
            {
                tileBuilder.Property(t => t.Key).HasMaxLength(100);
                tileBuilder.Property(t => t.Name).HasMaxLength(200);
                tileBuilder.Property(t => t.Points);
                tileBuilder.Property(t => t.RequiredCount);

                tileBuilder.OwnsMany(t => t.AllowedActivities, ruleBuilder =>
                {
                    ruleBuilder.Property(r => r.ActivityDefinitionId);
                    ruleBuilder.Property(r => r.ActivityKey).HasMaxLength(100);

                    ruleBuilder.OwnsMany(r => r.Requirements, reqBuilder =>
                    {
                        reqBuilder.Property(c => c.Key).HasMaxLength(100);
                        reqBuilder.Property(c => c.Name).HasMaxLength(200);
                    });

                    ruleBuilder.OwnsMany(r => r.Modifiers, modBuilder =>
                    {
                        modBuilder.OwnsOne(m => m.Capability, capBuilder =>
                        {
                            capBuilder.Property(c => c.Key).HasMaxLength(100);
                            capBuilder.Property(c => c.Name).HasMaxLength(200);
                        });
                        modBuilder.Property(m => m.TimeMultiplier).HasPrecision(9, 4);
                        modBuilder.Property(m => m.ProbabilityMultiplier).HasPrecision(9, 4);
                    });
                });
            });
        });
    }
}
