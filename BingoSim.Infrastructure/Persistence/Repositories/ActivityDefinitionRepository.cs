using BingoSim.Core.Entities;
using BingoSim.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IActivityDefinitionRepository.
/// </summary>
public class ActivityDefinitionRepository(AppDbContext context) : IActivityDefinitionRepository
{
    public async Task<ActivityDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.ActivityDefinitions
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ActivityDefinition>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return [];

        return await context.ActivityDefinitions
            .Where(e => idList.Contains(e.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<ActivityDefinition?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await context.ActivityDefinitions
            .FirstOrDefaultAsync(e => e.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<ActivityDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.ActivityDefinitions
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ActivityDefinition entity, CancellationToken cancellationToken = default)
    {
        await context.ActivityDefinitions.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ActivityDefinition entity, CancellationToken cancellationToken = default)
    {
        context.ActivityDefinitions.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.ActivityDefinitions
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is not null)
        {
            context.ActivityDefinitions.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.ActivityDefinitions
            .AnyAsync(e => e.Id == id, cancellationToken);
    }
}
