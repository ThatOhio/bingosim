using BingoSim.Core.Entities;
using BingoSim.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.Persistence.Repositories;

public class SimulationBatchRepository(AppDbContext context) : ISimulationBatchRepository
{
    public async Task<SimulationBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.SimulationBatches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task AddAsync(SimulationBatch batch, CancellationToken cancellationToken = default)
    {
        await context.SimulationBatches.AddAsync(batch, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SimulationBatch batch, CancellationToken cancellationToken = default)
    {
        context.SimulationBatches.Update(batch);
        await context.SaveChangesAsync(cancellationToken);
    }
}
