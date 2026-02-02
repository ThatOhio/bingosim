using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.Persistence.Repositories;

public class SimulationRunRepository(AppDbContext context) : ISimulationRunRepository
{
    public async Task<SimulationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.SimulationRuns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SimulationRun>> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        await context.SimulationRuns
            .Where(r => r.SimulationBatchId == batchId)
            .OrderBy(r => r.RunIndex)
            .ToListAsync(cancellationToken);

    public async Task AddRangeAsync(IEnumerable<SimulationRun> runs, CancellationToken cancellationToken = default)
    {
        await context.SimulationRuns.AddRangeAsync(runs, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SimulationRun run, CancellationToken cancellationToken = default)
    {
        context.SimulationRuns.Update(run);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryClaimAsync(Guid runId, DateTimeOffset startedAt, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await context.SimulationRuns
            .Where(r => r.Id == runId && r.Status == RunStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, RunStatus.Running)
                .SetProperty(r => r.StartedAt, startedAt)
                .SetProperty(r => r.LastAttemptAt, startedAt),
                cancellationToken);

        // ExecuteUpdateAsync bypasses EF Core's change tracker, so we need to detach
        // any tracked entity to ensure subsequent reads get fresh data from the database
        var trackedEntity = context.ChangeTracker.Entries<SimulationRun>()
            .FirstOrDefault(e => e.Entity.Id == runId);
        if (trackedEntity is not null)
            trackedEntity.State = EntityState.Detached;

        return rowsAffected > 0;
    }
}
