using BingoSim.Core.Entities;

namespace BingoSim.Core.Interfaces;

/// <summary>
/// Repository interface for EventSnapshot persistence.
/// </summary>
public interface IEventSnapshotRepository
{
    Task<EventSnapshot?> GetByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task AddAsync(EventSnapshot snapshot, CancellationToken cancellationToken = default);
}
