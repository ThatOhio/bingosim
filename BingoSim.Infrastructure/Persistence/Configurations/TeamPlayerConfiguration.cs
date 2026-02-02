using BingoSim.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BingoSim.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for TeamPlayer entity.
/// </summary>
public class TeamPlayerConfiguration : IEntityTypeConfiguration<TeamPlayer>
{
    public void Configure(EntityTypeBuilder<TeamPlayer> builder)
    {
        builder.ToTable("TeamPlayers");

        builder.HasKey(tp => tp.Id);

        builder.Property(tp => tp.TeamId)
            .IsRequired();

        builder.Property(tp => tp.PlayerProfileId)
            .IsRequired();

        builder.HasIndex(tp => new { tp.TeamId, tp.PlayerProfileId })
            .IsUnique();

        builder.HasOne(tp => tp.PlayerProfile)
            .WithMany()
            .HasForeignKey(tp => tp.PlayerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
