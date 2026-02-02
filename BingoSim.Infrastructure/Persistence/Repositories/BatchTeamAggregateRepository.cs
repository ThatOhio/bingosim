using BingoSim.Core.Entities;
using BingoSim.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.Persistence.Repositories;

public class BatchTeamAggregateRepository(AppDbContext context) : IBatchTeamAggregateRepository
{
    public async Task AddRangeAsync(IEnumerable<BatchTeamAggregate> aggregates, CancellationToken cancellationToken = default)
    {
        await context.BatchTeamAggregates.AddRangeAsync(aggregates, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BatchTeamAggregate>> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        await context.BatchTeamAggregates
            .Where(a => a.SimulationBatchId == batchId)
            .ToListAsync(cancellationToken);

    public async Task DeleteByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var toDelete = await context.BatchTeamAggregates.Where(a => a.SimulationBatchId == batchId).ToListAsync(cancellationToken);
        context.BatchTeamAggregates.RemoveRange(toDelete);
        await context.SaveChangesAsync(cancellationToken);
    }
}
