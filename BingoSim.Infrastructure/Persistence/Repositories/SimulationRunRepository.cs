using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.Persistence.Repositories;

public class SimulationRunRepository(AppDbContext context) : ISimulationRunRepository
{
    public async Task<SimulationRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.SimulationRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SimulationRun>> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        await context.SimulationRuns
            .AsNoTracking()
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

    public async Task<IReadOnlyList<Guid>> ClaimBatchAsync(
        IReadOnlyList<Guid> runIds,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken = default)
    {
        if (runIds.Count == 0)
            return [];

        var ids = runIds.ToArray();
        var pendingStatus = RunStatus.Pending.ToString();
        var runningStatus = RunStatus.Running.ToString();

        var claimedIds = await context.Database
            .SqlQuery<Guid>($"""
                UPDATE "SimulationRuns"
                SET "Status" = {runningStatus}, "StartedAt" = {startedAt}, "LastAttemptAt" = {startedAt}
                WHERE "Id" = ANY({ids}) AND "Status" = {pendingStatus}
                RETURNING "Id"
                """)
            .ToListAsync(cancellationToken);

        foreach (var runId in claimedIds)
        {
            var tracked = context.ChangeTracker.Entries<SimulationRun>().FirstOrDefault(e => e.Entity.Id == runId);
            if (tracked is not null)
                tracked.State = EntityState.Detached;
        }

        return claimedIds;
    }

    public async Task<int> BulkMarkCompletedAsync(IReadOnlyList<Guid> runIds, DateTimeOffset completedAt, CancellationToken cancellationToken = default)
    {
        if (runIds.Count == 0)
            return 0;

        // Update Pending (local perf path) or Running (distributed path) to Completed
        var rowsAffected = await context.SimulationRuns
            .Where(r => runIds.Contains(r.Id) && (r.Status == RunStatus.Pending || r.Status == RunStatus.Running))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, RunStatus.Completed)
                .SetProperty(r => r.CompletedAt, completedAt),
                cancellationToken);

        foreach (var runId in runIds)
        {
            var tracked = context.ChangeTracker.Entries<SimulationRun>().FirstOrDefault(e => e.Entity.Id == runId);
            if (tracked is not null)
                tracked.State = EntityState.Detached;
        }

        return rowsAffected;
    }

    public async Task<int> ResetStuckRunsToPendingAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await context.SimulationRuns
            .Where(r => r.SimulationBatchId == batchId && r.Status == RunStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, RunStatus.Pending)
                .SetProperty(r => r.StartedAt, (DateTimeOffset?)null)
                .SetProperty(r => r.LastAttemptAt, (DateTimeOffset?)null),
                cancellationToken);
        return rowsAffected;
    }

    public async Task<bool> HasNonTerminalRunsAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return await context.SimulationRuns
            .AsNoTracking()
            .Where(r => r.SimulationBatchId == batchId && (r.Status == RunStatus.Pending || r.Status == RunStatus.Running))
            .AnyAsync(cancellationToken);
    }
}
