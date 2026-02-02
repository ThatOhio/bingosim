using BingoSim.Core.Entities;
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
}
