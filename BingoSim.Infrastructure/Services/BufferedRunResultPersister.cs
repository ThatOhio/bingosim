using System.Collections.Concurrent;
using BingoSim.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Core.Entities;
using BingoSim.Core.Interfaces;
using BingoSim.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BingoSim.Infrastructure.Services;

/// <summary>
/// Buffers completed run results and flushes in batches. Uses fresh DbContext per flush.
/// Flush triggers: count >= BatchSize OR time since last flush >= FlushIntervalMs.
/// </summary>
public sealed class BufferedRunResultPersister(
    IServiceScopeFactory scopeFactory,
    IOptions<SimulationPersistenceOptions> options,
    ILogger<BufferedRunResultPersister> logger) : IBufferedRunResultPersister
{
    private readonly SimulationPersistenceOptions _options = options.Value;
    private readonly List<BufferedItem> _buffer = [];
    private readonly object _bufferLock = new();
    private DateTimeOffset _lastFlushAt = DateTimeOffset.UtcNow;

    private readonly ConcurrentDictionary<string, (int FlushCount, long RowsInserted, int RowsUpdated, int SaveChangesCount, long ElapsedMs)> _stats = new();

    private sealed record BufferedItem(
        Guid RunId,
        Guid BatchId,
        DateTimeOffset CompletedAt,
        bool IsRetry,
        IReadOnlyList<TeamRunResultDto> Results);

    public async Task AddAsync(
        Guid runId,
        Guid batchId,
        DateTimeOffset completedAt,
        bool isRetry,
        IReadOnlyList<TeamRunResultDto> results,
        CancellationToken cancellationToken = default)
    {
        List<BufferedItem>? toFlush = null;
        lock (_bufferLock)
        {
            _buffer.Add(new BufferedItem(runId, batchId, completedAt, isRetry, results));

            var shouldFlush = _buffer.Count >= _options.BatchSize
                || ( _options.FlushIntervalMs > 0
                    && (DateTimeOffset.UtcNow - _lastFlushAt).TotalMilliseconds >= _options.FlushIntervalMs);

            if (shouldFlush && _buffer.Count > 0)
            {
                toFlush = [.. _buffer];
                _buffer.Clear();
                _lastFlushAt = DateTimeOffset.UtcNow;
            }
        }

        if (toFlush is not null)
            await FlushInternalAsync(toFlush, cancellationToken);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        List<BufferedItem>? toFlush;
        lock (_bufferLock)
        {
            if (_buffer.Count == 0)
                return;
            toFlush = [.. _buffer];
            _buffer.Clear();
            _lastFlushAt = DateTimeOffset.UtcNow;
        }

        await FlushInternalAsync(toFlush, cancellationToken);
    }

    public BufferedPersistStats GetStats()
    {
        var key = "default";
        if (_stats.TryGetValue(key, out var s))
            return new BufferedPersistStats(s.FlushCount, s.RowsInserted, s.RowsUpdated, s.SaveChangesCount, s.ElapsedMs);
        return new BufferedPersistStats(0, 0, 0, 0, 0);
    }

    private async Task FlushInternalAsync(List<BufferedItem> items, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var runIds = items.Select(i => i.RunId).ToList();
        var retryRunIds = items.Where(i => i.IsRetry).Select(i => i.RunId).ToList();

        var entities = new List<TeamRunResult>();
        foreach (var item in items)
        {
            foreach (var r in item.Results)
            {
                entities.Add(new TeamRunResult(
                    item.RunId,
                    r.TeamId,
                    r.TeamName,
                    r.StrategyKey,
                    r.ParamsJson,
                    r.TotalPoints,
                    r.TilesCompletedCount,
                    r.RowReached,
                    r.IsWinner,
                    r.RowUnlockTimesJson,
                    r.TileCompletionTimesJson));
            }
        }

        var rowsInserted = entities.Count;
        var rowsUpdated = 0;
        var saveChangesCount = 0;

        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var runRepo = scope.ServiceProvider.GetRequiredService<ISimulationRunRepository>();
        var resultRepo = scope.ServiceProvider.GetRequiredService<ITeamRunResultRepository>();

        var originalAutoDetect = context.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            if (retryRunIds.Count > 0)
                await resultRepo.DeleteByRunIdsAsync(retryRunIds, cancellationToken);

            await context.TeamRunResults.AddRangeAsync(entities, cancellationToken);
            rowsUpdated = await runRepo.BulkMarkCompletedAsync(runIds, DateTimeOffset.UtcNow, cancellationToken);
            saveChangesCount = 1;
            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            context.ChangeTracker.AutoDetectChangesEnabled = originalAutoDetect;
        }

        sw.Stop();

        var perfRecorder = scope.ServiceProvider.GetService<IPerfRecorder>();
        perfRecorder?.Record("persist", sw.ElapsedMilliseconds, items.Count);

        _stats.AddOrUpdate("default",
            _ => (1, rowsInserted, rowsUpdated, saveChangesCount, sw.ElapsedMilliseconds),
            (_, prev) => (
                prev.FlushCount + 1,
                prev.RowsInserted + rowsInserted,
                prev.RowsUpdated + rowsUpdated,
                prev.SaveChangesCount + saveChangesCount,
                prev.ElapsedMs + sw.ElapsedMilliseconds));

        logger.LogDebug("Flush: {RunCount} runs, {RowsInserted} rows inserted, {RowsUpdated} runs updated, {ElapsedMs}ms",
            items.Count, rowsInserted, rowsUpdated, sw.ElapsedMilliseconds);

        var finalizationService = scope.ServiceProvider.GetRequiredService<IBatchFinalizationService>();
        foreach (var batchId in items.Select(i => i.BatchId).Distinct())
            await finalizationService.TryFinalizeAsync(batchId, cancellationToken);
    }
}

/// <summary>
/// Options for batched persistence.
/// </summary>
public class SimulationPersistenceOptions
{
    public const string SectionName = "SimulationPersistence";

    /// <summary>Flush when buffer reaches this many runs. Default 100.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Flush when this many ms elapsed since last flush. 0 = count-based only. Default 500.</summary>
    public int FlushIntervalMs { get; set; } = 500;
}
