using BingoSim.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.Persistence;

/// <summary>
/// Application database context.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<ActivityDefinition> ActivityDefinitions => Set<ActivityDefinition>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamPlayer> TeamPlayers => Set<TeamPlayer>();
    public DbSet<StrategyConfig> StrategyConfigs => Set<StrategyConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
