using BingoSim.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BingoSim.Infrastructure.Persistence.Configurations;

public class TeamRunResultConfiguration : IEntityTypeConfiguration<TeamRunResult>
{
    public void Configure(EntityTypeBuilder<TeamRunResult> builder)
    {
        builder.ToTable("TeamRunResults");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.SimulationRunId).IsRequired();
        builder.Property(r => r.TeamId).IsRequired();
        builder.Property(r => r.TeamName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.StrategyKey).IsRequired().HasMaxLength(100);
        builder.Property(r => r.StrategyParamsJson).HasMaxLength(8000);
        builder.Property(r => r.TotalPoints).IsRequired();
        builder.Property(r => r.TilesCompletedCount).IsRequired();
        builder.Property(r => r.RowReached).IsRequired();
        builder.Property(r => r.IsWinner).IsRequired();
        builder.Property(r => r.RowUnlockTimesJson).IsRequired().HasColumnType("jsonb");
        builder.Property(r => r.TileCompletionTimesJson).IsRequired().HasColumnType("jsonb");
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasIndex(r => r.SimulationRunId);
    }
}
