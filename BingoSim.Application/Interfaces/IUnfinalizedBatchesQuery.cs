namespace BingoSim.Application.Interfaces;

/// <summary>
/// Returns batch IDs that need finalization: Status Pending/Running and all runs terminal.
/// </summary>
public interface IUnfinalizedBatchesQuery
{
    Task<IReadOnlyList<Guid>> GetBatchIdsAsync(CancellationToken cancellationToken = default);
}
