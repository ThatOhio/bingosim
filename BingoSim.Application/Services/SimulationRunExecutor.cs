using BingoSim.Application.Interfaces;
using BingoSim.Application.Simulation;
using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BingoSim.Application.Services;

/// <summary>
/// Executes one simulation run: load run + snapshot, run simulation, persist TeamRunResult, update run status.
/// Uses atomic claim (TryClaimAsync) to prevent double execution. Retries up to 5 attempts per run via work publisher.
/// Batch finalization delegated to IBatchFinalizationService (idempotent).
/// Uses shared snapshot cache (ISnapshotCache) to eliminate redundant DB loads across runs from the same batch.
/// </summary>
public class SimulationRunExecutor(
    ISimulationRunRepository runRepo,
    IEventSnapshotRepository snapshotRepo,
    ISnapshotCache snapshotCache,
    ITeamRunResultRepository resultRepo,
    ISimulationRunWorkPublisher workPublisher,
    IBatchFinalizationService finalizationService,
    SimulationRunner runner,
    ILogger<SimulationRunExecutor> logger,
    IBufferedRunResultPersister? bufferedPersister = null,
    ISimulationPersistenceConfig? persistenceConfig = null,
    ISimulationMetrics? metrics = null,
    IPerfRecorder? perfRecorder = null,
    IPerfScenarioOptions? perfOptions = null) : ISimulationRunExecutor
{
    private const int MaxErrorLength = 500;
    private const int VerboseLogEveryNIterations = 1000;

    public async Task ExecuteAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await runRepo.GetByIdAsync(runId, cancellationToken);
        if (run is null)
        {
            logger.LogWarning("Run {RunId} not found", runId);
            return;
        }

        if (run.IsTerminal)
        {
            logger.LogDebug("Run {RunId} already terminal", runId);
            return;
        }

        var startedAt = DateTimeOffset.UtcNow;
        var claimSw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (!await runRepo.TryClaimAsync(runId, startedAt, cancellationToken))
            {
                logger.LogDebug("Run {RunId} already claimed by another worker", runId);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ClaimDbError] TryClaimAsync failed for run {RunId}", runId);
            throw;
        }
        claimSw.Stop();
        perfRecorder?.Record("claim", claimSw.ElapsedMilliseconds, 1);

        run.MarkRunning(startedAt);

        EventSnapshotDto snapshot;
        var cached = snapshotCache.Get(run.SimulationBatchId);
        if (cached is not null)
        {
            perfRecorder?.Record("snapshot_cache_hit", 0, 1);
            snapshot = cached;
        }
        else
        {
            perfRecorder?.Record("snapshot_cache_miss", 0, 1);
            var snapshotLoadSw = System.Diagnostics.Stopwatch.StartNew();
            var snapshotEntity = await snapshotRepo.GetByBatchIdAsync(run.SimulationBatchId, cancellationToken);
            snapshotLoadSw.Stop();
            perfRecorder?.Record("snapshot_load", snapshotLoadSw.ElapsedMilliseconds, 1);

            if (snapshotEntity is null)
            {
                await FailRunAsync(run, "Snapshot not found", cancellationToken);
                return;
            }

            var snapshotJson = perfOptions is { UseSyntheticSnapshot: true }
                ? PerfScenarioSnapshot.BuildJson()
                : snapshotEntity.EventConfigJson;

            var dto = EventSnapshotBuilder.Deserialize(snapshotJson);
            if (dto is null)
            {
                await FailRunAsync(run, "Snapshot JSON invalid", cancellationToken);
                return;
            }
            SnapshotValidator.Validate(dto);
            snapshotCache.Set(run.SimulationBatchId, dto);
            snapshot = dto;

            if (perfOptions?.DumpSnapshotPath is { } dumpPath)
            {
                var path = dumpPath.Contains("{0}") ? string.Format(dumpPath, run.SimulationBatchId) : dumpPath;
                await System.IO.File.WriteAllTextAsync(path, snapshotJson, cancellationToken);
                logger.LogInformation("Dumped snapshot to {Path}", path);
            }
        }

        logger.LogDebug("Executing run {RunId} for batch {BatchId} (attempt {Attempt})",
            run.Id, run.SimulationBatchId, run.AttemptCount + 1);

        await ExecuteWithSnapshotAsync(run, snapshot, cancellationToken);
    }

    public async Task ExecuteAsync(SimulationRun run, EventSnapshotDto snapshot, CancellationToken cancellationToken = default)
    {
        if (run.IsTerminal)
        {
            logger.LogDebug("Run {RunId} already terminal", run.Id);
            return;
        }

        run.MarkRunning(DateTimeOffset.UtcNow);

        logger.LogDebug("Executing run {RunId} for batch {BatchId} (attempt {Attempt})",
            run.Id, run.SimulationBatchId, run.AttemptCount + 1);

        await ExecuteWithSnapshotAsync(run, snapshot, cancellationToken);
    }

    private async Task ExecuteWithSnapshotAsync(SimulationRun run, EventSnapshotDto snapshot, CancellationToken cancellationToken)
    {
        try
        {
            var simSw = System.Diagnostics.Stopwatch.StartNew();
            var progressReporter = perfOptions is { Verbose: true }
                ? new VerboseProgressReporter(VerboseLogEveryNIterations)
                : null;
            var results = runner.Execute(snapshot, run.Seed, cancellationToken, progressReporter);
            simSw.Stop();
            perfRecorder?.Record("sim", simSw.ElapsedMilliseconds, 1);
            logger.LogDebug("Run {RunId} simulation completed in {ElapsedMs}ms ({TeamCount} teams)",
                run.Id, simSw.ElapsedMilliseconds, results.Count);

            var completedAt = DateTimeOffset.UtcNow;
            run.MarkCompleted(completedAt);
            var useBuffer = bufferedPersister is not null && persistenceConfig is not null && persistenceConfig.BatchSize > 1;

            if (useBuffer)
            {
                await bufferedPersister!.AddAsync(
                    run.Id,
                    run.SimulationBatchId,
                    completedAt,
                    isRetry: run.AttemptCount > 0,
                    results,
                    cancellationToken);
                // Persist time recorded by BufferedRunResultPersister on each flush
            }
            else
            {
                var dbSw = System.Diagnostics.Stopwatch.StartNew();
                if (run.AttemptCount > 0)
                    await resultRepo.DeleteByRunIdAsync(run.Id, cancellationToken);
                var entities = results.Select(r => new TeamRunResult(
                    run.Id,
                    r.TeamId,
                    r.TeamName,
                    r.StrategyKey,
                    r.ParamsJson,
                    r.TotalPoints,
                    r.TilesCompletedCount,
                    r.RowReached,
                    r.IsWinner,
                    r.RowUnlockTimesJson,
                    r.TileCompletionTimesJson)).ToList();
                await resultRepo.AddRangeAsync(entities, cancellationToken);
                await runRepo.UpdateAsync(run, cancellationToken);
                dbSw.Stop();
                perfRecorder?.Record("persist", dbSw.ElapsedMilliseconds, 1);
                await finalizationService.TryFinalizeAsync(run.SimulationBatchId, cancellationToken);
            }

            logger.LogDebug("Run {RunId} DB ops completed for batch {BatchId}", run.Id, run.SimulationBatchId);
            metrics?.RecordRunCompleted(run.SimulationBatchId, run.Id);
        }
        catch (Exception ex)
        {
            var message = ex.Message.Length > MaxErrorLength ? ex.Message[..MaxErrorLength] + "..." : ex.Message;
            run.MarkFailed(message, DateTimeOffset.UtcNow);
            await runRepo.UpdateAsync(run, cancellationToken);
            if (run.IsTerminal)
                metrics?.RecordRunFailed(run.SimulationBatchId, run.Id);
            else
                metrics?.RecordRunRetried(run.SimulationBatchId, run.Id);
            logger.LogError(ex, "Run {RunId} failed for batch {BatchId} (attempt {Attempt}): {Message}",
                run.Id, run.SimulationBatchId, run.AttemptCount, message);
            if (!run.IsTerminal)
                await workPublisher.PublishRunWorkAsync(run.Id, cancellationToken);
            await finalizationService.TryFinalizeAsync(run.SimulationBatchId, cancellationToken);
        }
    }

    private async Task FailRunAsync(SimulationRun run, string message, CancellationToken cancellationToken)
    {
        run.MarkFailed(message, DateTimeOffset.UtcNow);
        await runRepo.UpdateAsync(run, cancellationToken);
        if (run.IsTerminal)
            metrics?.RecordRunFailed(run.SimulationBatchId, run.Id);
        else
            metrics?.RecordRunRetried(run.SimulationBatchId, run.Id);
        if (!run.IsTerminal)
            await workPublisher.PublishRunWorkAsync(run.Id, cancellationToken);
        await finalizationService.TryFinalizeAsync(run.SimulationBatchId, cancellationToken);
    }
}
