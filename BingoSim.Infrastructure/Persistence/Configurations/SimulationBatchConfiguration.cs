using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BingoSim.Infrastructure.Persistence.Configurations;

public class SimulationBatchConfiguration : IEntityTypeConfiguration<SimulationBatch>
{
    public void Configure(EntityTypeBuilder<SimulationBatch> builder)
    {
        builder.ToTable("SimulationBatches");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.EventId).IsRequired();
        builder.Property(b => b.Name).HasMaxLength(200);
        builder.Property(b => b.RunsRequested).IsRequired();
        builder.Property(b => b.Seed).IsRequired().HasMaxLength(100);
        builder.Property(b => b.ExecutionMode).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.ErrorMessage).HasMaxLength(1000);
        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.CompletedAt);

        builder.HasIndex(b => b.EventId);
        builder.HasIndex(b => b.Status);
    }
}
