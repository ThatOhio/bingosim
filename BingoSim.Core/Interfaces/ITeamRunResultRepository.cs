using BingoSim.Core.Entities;

namespace BingoSim.Core.Interfaces;

/// <summary>
/// Repository interface for TeamRunResult persistence.
/// </summary>
public interface ITeamRunResultRepository
{
    Task AddRangeAsync(IEnumerable<TeamRunResult> results, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamRunResult>> GetByRunIdAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamRunResult>> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task DeleteByRunIdAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete results for multiple runs (for retry cleanup in batched flush).
    /// </summary>
    Task<int> DeleteByRunIdsAsync(IReadOnlyList<Guid> runIds, CancellationToken cancellationToken = default);
}
