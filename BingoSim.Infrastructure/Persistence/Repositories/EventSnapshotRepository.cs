using BingoSim.Core.Entities;
using BingoSim.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.Persistence.Repositories;

public class EventSnapshotRepository(AppDbContext context) : IEventSnapshotRepository
{
    public async Task<EventSnapshot?> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        await context.EventSnapshots.FirstOrDefaultAsync(s => s.SimulationBatchId == batchId, cancellationToken);

    public async Task AddAsync(EventSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await context.EventSnapshots.AddAsync(snapshot, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
