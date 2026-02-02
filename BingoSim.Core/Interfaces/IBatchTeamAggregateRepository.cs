using BingoSim.Core.Entities;

namespace BingoSim.Core.Interfaces;

/// <summary>
/// Repository interface for BatchTeamAggregate persistence.
/// </summary>
public interface IBatchTeamAggregateRepository
{
    Task AddRangeAsync(IEnumerable<BatchTeamAggregate> aggregates, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BatchTeamAggregate>> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task DeleteByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);
}

