using BingoSim.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BingoSim.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Team entity.
/// </summary>
public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.EventId)
            .IsRequired();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.HasIndex(t => t.EventId);

        builder.HasOne(t => t.StrategyConfig)
            .WithOne(s => s.Team!)
            .HasForeignKey<StrategyConfig>(s => s.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.TeamPlayers)
            .WithOne(tp => tp.Team)
            .HasForeignKey(tp => tp.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(t => t.TeamPlayers).HasField("_teamPlayers");
    }
}
