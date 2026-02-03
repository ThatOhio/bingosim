using BingoSim.Application.Interfaces;
using BingoSim.Core.Enums;
using BingoSim.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.Queries;

/// <summary>
/// Returns batch IDs needing finalization: Status Pending/Running and all runs terminal.
/// </summary>
public class UnfinalizedBatchesQuery(AppDbContext context) : IUnfinalizedBatchesQuery
{
    public async Task<IReadOnlyList<Guid>> GetBatchIdsAsync(CancellationToken cancellationToken = default)
    {
        return await context.SimulationBatches
            .Where(b => b.Status == BatchStatus.Pending || b.Status == BatchStatus.Running)
            .Where(b => !context.SimulationRuns.Any(r =>
                r.SimulationBatchId == b.Id
                && r.Status != RunStatus.Completed
                && r.Status != RunStatus.Failed))
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);
    }
}
