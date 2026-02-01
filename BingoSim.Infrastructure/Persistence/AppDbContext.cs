using BingoSim.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.Persistence;

/// <summary>
/// Application database context.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
