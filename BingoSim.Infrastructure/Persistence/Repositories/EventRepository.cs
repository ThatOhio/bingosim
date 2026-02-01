using BingoSim.Core.Entities;
using BingoSim.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IEventRepository.
/// </summary>
public class EventRepository(AppDbContext context) : IEventRepository
{
    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Events
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Event>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Events
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Event entity, CancellationToken cancellationToken = default)
    {
        await context.Events.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Event entity, CancellationToken cancellationToken = default)
    {
        context.Events.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.Events
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is not null)
        {
            context.Events.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Events
            .AnyAsync(e => e.Id == id, cancellationToken);
    }
}
