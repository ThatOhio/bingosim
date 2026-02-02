using BingoSim.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BingoSim.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for StrategyConfig entity.
/// </summary>
public class StrategyConfigConfiguration : IEntityTypeConfiguration<StrategyConfig>
{
    public void Configure(EntityTypeBuilder<StrategyConfig> builder)
    {
        builder.ToTable("StrategyConfigs");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TeamId)
            .IsRequired();

        builder.Property(s => s.StrategyKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.ParamsJson)
            .HasMaxLength(8000);

        builder.HasIndex(s => s.TeamId)
            .IsUnique();
    }
}
