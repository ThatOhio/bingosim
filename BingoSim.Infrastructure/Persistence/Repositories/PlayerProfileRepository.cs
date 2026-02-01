using BingoSim.Core.Entities;
using BingoSim.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IPlayerProfileRepository.
/// </summary>
public class PlayerProfileRepository(AppDbContext context) : IPlayerProfileRepository
{
    public async Task<PlayerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.PlayerProfiles
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.PlayerProfiles
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PlayerProfile profile, CancellationToken cancellationToken = default)
    {
        await context.PlayerProfiles.AddAsync(profile, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PlayerProfile profile, CancellationToken cancellationToken = default)
    {
        context.PlayerProfiles.Update(profile);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await context.PlayerProfiles
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (profile is not null)
        {
            context.PlayerProfiles.Remove(profile);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.PlayerProfiles
            .AnyAsync(p => p.Id == id, cancellationToken);
    }
}
