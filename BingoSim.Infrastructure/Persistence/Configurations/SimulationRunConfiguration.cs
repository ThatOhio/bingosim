using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BingoSim.Infrastructure.Persistence.Configurations;

public class SimulationRunConfiguration : IEntityTypeConfiguration<SimulationRun>
{
    public void Configure(EntityTypeBuilder<SimulationRun> builder)
    {
        builder.ToTable("SimulationRuns");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.SimulationBatchId).IsRequired();
        builder.Property(r => r.RunIndex).IsRequired();
        builder.Property(r => r.Seed).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.AttemptCount).IsRequired();
        builder.Property(r => r.LastError).HasMaxLength(600);
        builder.Property(r => r.LastAttemptAt);
        builder.Property(r => r.StartedAt);
        builder.Property(r => r.CompletedAt);

        builder.HasIndex(r => r.SimulationBatchId);
    }
}
