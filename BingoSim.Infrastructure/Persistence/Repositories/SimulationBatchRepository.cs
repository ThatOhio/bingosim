using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Infrastructure.Persistence.Repositories;

public class SimulationBatchRepository(AppDbContext context) : ISimulationBatchRepository
{
    public async Task<SimulationBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.SimulationBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

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

    public async Task<bool> TryTransitionToFinalAsync(Guid id, BatchStatus newStatus, DateTimeOffset completedAt, string? errorMessage, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await context.SimulationBatches
            .Where(b => b.Id == id && (b.Status == BatchStatus.Pending || b.Status == BatchStatus.Running))
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, newStatus)
                .SetProperty(b => b.CompletedAt, completedAt)
                .SetProperty(b => b.ErrorMessage, errorMessage),
                cancellationToken);
        return rowsAffected > 0;
    }
}
