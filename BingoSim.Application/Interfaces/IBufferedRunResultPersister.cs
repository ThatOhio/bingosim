using BingoSim.Application.Simulation.Runner;

namespace BingoSim.Application.Interfaces;

/// <summary>
/// Buffers completed run results and flushes in batches to reduce SaveChanges calls.
/// Used by local and distributed executors when batch size > 1.
/// </summary>
public interface IBufferedRunResultPersister
{
    /// <summary>
    /// Add completed run results to buffer. May trigger flush when count or time threshold reached.
    /// </summary>
    Task AddAsync(
        Guid runId,
        Guid batchId,
        DateTimeOffset completedAt,
        bool isRetry,
        IReadOnlyList<TeamRunResultDto> results,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Force flush any buffered results. Call at batch end or shutdown.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get instrumentation stats: flush count, rows inserted, rows updated, SaveChanges count.
    /// </summary>
    BufferedPersistStats GetStats();
}

/// <summary>
/// Instrumentation stats for batched persistence.
/// </summary>
public sealed record BufferedPersistStats(
    int FlushCount,
    long RowsInserted,
    int RowsUpdated,
    int SaveChangesCount,
    long ElapsedMsTotal);
