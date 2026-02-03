using BingoSim.Core.Entities;
using BingoSim.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.Persistence.Repositories;

public class TeamRunResultRepository(AppDbContext context) : ITeamRunResultRepository
{
    public async Task AddRangeAsync(IEnumerable<TeamRunResult> results, CancellationToken cancellationToken = default)
    {
        await context.TeamRunResults.AddRangeAsync(results, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TeamRunResult>> GetByRunIdAsync(Guid runId, CancellationToken cancellationToken = default) =>
        await context.TeamRunResults
            .AsNoTracking()
            .Where(r => r.SimulationRunId == runId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TeamRunResult>> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var runIds = await context.SimulationRuns
            .AsNoTracking()
            .Where(r => r.SimulationBatchId == batchId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        if (runIds.Count == 0)
            return [];
        return await context.TeamRunResults
            .AsNoTracking()
            .Where(r => runIds.Contains(r.SimulationRunId))
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteByRunIdAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var toDelete = await context.TeamRunResults.Where(r => r.SimulationRunId == runId).ToListAsync(cancellationToken);
        context.TeamRunResults.RemoveRange(toDelete);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteByRunIdsAsync(IReadOnlyList<Guid> runIds, CancellationToken cancellationToken = default)
    {
        if (runIds.Count == 0)
            return 0;

        return await context.TeamRunResults
            .Where(r => runIds.Contains(r.SimulationRunId))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
