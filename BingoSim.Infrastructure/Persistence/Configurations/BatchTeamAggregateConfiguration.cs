using BingoSim.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BingoSim.Infrastructure.Persistence.Configurations;

public class BatchTeamAggregateConfiguration : IEntityTypeConfiguration<BatchTeamAggregate>
{
    public void Configure(EntityTypeBuilder<BatchTeamAggregate> builder)
    {
        builder.ToTable("BatchTeamAggregates");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.SimulationBatchId).IsRequired();
        builder.Property(a => a.TeamId).IsRequired();
        builder.Property(a => a.TeamName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.StrategyKey).IsRequired().HasMaxLength(100);
        builder.Property(a => a.MeanPoints).IsRequired();
        builder.Property(a => a.MinPoints).IsRequired();
        builder.Property(a => a.MaxPoints).IsRequired();
        builder.Property(a => a.MeanTilesCompleted).IsRequired();
        builder.Property(a => a.MinTilesCompleted).IsRequired();
        builder.Property(a => a.MaxTilesCompleted).IsRequired();
        builder.Property(a => a.MeanRowReached).IsRequired();
        builder.Property(a => a.MinRowReached).IsRequired();
        builder.Property(a => a.MaxRowReached).IsRequired();
        builder.Property(a => a.WinnerRate).IsRequired();
        builder.Property(a => a.RunCount).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasIndex(a => a.SimulationBatchId);
    }
}
