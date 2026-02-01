using BingoSim.Core.Entities;
using BingoSim.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BingoSim.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ActivityDefinition entity.
/// </summary>
public class ActivityDefinitionConfiguration : IEntityTypeConfiguration<ActivityDefinition>
{
    public void Configure(EntityTypeBuilder<ActivityDefinition> builder)
    {
        builder.ToTable("ActivityDefinitions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.HasIndex(e => e.Key)
            .IsUnique();

        builder.HasIndex(e => e.CreatedAt)
            .IsDescending();

        // ModeSupport as JSON
        builder.OwnsOne(e => e.ModeSupport, modeBuilder =>
        {
            modeBuilder.ToJson("ModeSupport");
            modeBuilder.Property(m => m.SupportsSolo);
            modeBuilder.Property(m => m.SupportsGroup);
            modeBuilder.Property(m => m.MinGroupSize);
            modeBuilder.Property(m => m.MaxGroupSize);
        });

        // Attempts as JSON (nested: TimeModel, Outcomes with Grants)
        builder.Navigation(e => e.Attempts).HasField("_attempts");
        builder.OwnsMany(e => e.Attempts, attemptBuilder =>
        {
            attemptBuilder.ToJson("Attempts");
            attemptBuilder.Property(a => a.Key).HasMaxLength(100);
            attemptBuilder.Property(a => a.RollScope).HasConversion<int>();

            attemptBuilder.OwnsOne(a => a.TimeModel, timeBuilder =>
            {
                timeBuilder.Property(t => t.BaselineTimeSeconds);
                timeBuilder.Property(t => t.Distribution).HasConversion<int>();
                timeBuilder.Property(t => t.VarianceSeconds);
            });

            attemptBuilder.OwnsMany(a => a.Outcomes, outcomeBuilder =>
            {
                outcomeBuilder.Property(o => o.Key).HasMaxLength(100);
                outcomeBuilder.Property(o => o.WeightNumerator);
                outcomeBuilder.Property(o => o.WeightDenominator);
                outcomeBuilder.OwnsMany(o => o.Grants, grantBuilder =>
                {
                    grantBuilder.Property(g => g.DropKey).HasMaxLength(200);
                    grantBuilder.Property(g => g.Units);
                });
            });
        });

        // GroupScalingBands as JSON
        builder.Navigation(e => e.GroupScalingBands).HasField("_groupScalingBands");
        builder.OwnsMany(e => e.GroupScalingBands, bandBuilder =>
        {
            bandBuilder.ToJson("GroupScalingBands");
            bandBuilder.Property(b => b.MinSize);
            bandBuilder.Property(b => b.MaxSize);
            bandBuilder.Property(b => b.TimeMultiplier).HasPrecision(9, 4);
            bandBuilder.Property(b => b.ProbabilityMultiplier).HasPrecision(9, 4);
        });
    }
}
