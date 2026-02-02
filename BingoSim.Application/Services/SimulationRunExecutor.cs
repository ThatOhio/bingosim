using BingoSim.Application.Interfaces;
using BingoSim.Application.Simulation.Runner;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BingoSim.Application.Services;

/// <summary>
/// Executes one simulation run: load run + snapshot, run simulation, persist TeamRunResult, update run status; on batch complete, compute and persist BatchTeamAggregate.
/// Retries up to 5 attempts per run; re-enqueues run when non-terminal failure.
/// </summary>
public class SimulationRunExecutor(
    ISimulationRunRepository runRepo,
    IEventSnapshotRepository snapshotRepo,
    ITeamRunResultRepository resultRepo,
    IBatchTeamAggregateRepository aggregateRepo,
    ISimulationBatchRepository batchRepo,
    ISimulationRunQueue runQueue,
    SimulationRunner runner,
    ILogger<SimulationRunExecutor> logger,
    ISimulationMetrics? metrics = null) : ISimulationRunExecutor
{
    private const int MaxErrorLength = 500;

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

        var snapshot = await snapshotRepo.GetByBatchIdAsync(run.SimulationBatchId, cancellationToken);
        if (snapshot is null)
        {
            await FailRunAsync(run, "Snapshot not found", cancellationToken);
            return;
        }

        run.MarkRunning(DateTimeOffset.UtcNow);
        await runRepo.UpdateAsync(run, cancellationToken);

        logger.LogInformation("Executing run {RunId} for batch {BatchId}", run.Id, run.SimulationBatchId);

        try
        {
            var results = runner.Execute(snapshot.EventConfigJson, run.Seed, cancellationToken);

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

            run.MarkCompleted(DateTimeOffset.UtcNow);
            await runRepo.UpdateAsync(run, cancellationToken);
            metrics?.RecordRunCompleted(run.SimulationBatchId, run.Id);
            logger.LogInformation("Run {RunId} completed", run.Id);

            await TryCompleteBatchAsync(run.SimulationBatchId, cancellationToken);
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
            logger.LogError(ex, "Run {RunId} failed (attempt {Attempt}): {Message}", run.Id, run.AttemptCount, message);
            if (!run.IsTerminal)
                await runQueue.EnqueueAsync(run.Id, cancellationToken);
            await TryCompleteBatchAsync(run.SimulationBatchId, cancellationToken);
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
            await runQueue.EnqueueAsync(run.Id, cancellationToken);
        await TryCompleteBatchAsync(run.SimulationBatchId, cancellationToken);
    }

    private async Task TryCompleteBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var runs = await runRepo.GetByBatchIdAsync(batchId, cancellationToken);
        if (runs.Any(r => !r.IsTerminal))
            return;

        var batch = await batchRepo.GetByIdAsync(batchId, cancellationToken);
        if (batch is null)
            return;

        var failedCount = runs.Count(r => r.Status == RunStatus.Failed);
        if (failedCount > 0)
        {
            batch.SetError($"One or more runs failed after 5 attempts ({failedCount} failed).", DateTimeOffset.UtcNow);
            await batchRepo.UpdateAsync(batch, cancellationToken);
            var duration = (batch.CompletedAt ?? DateTimeOffset.UtcNow) - batch.CreatedAt;
            metrics?.RecordBatchCompleted(batch.Id, duration);
            return;
        }

        var results = await resultRepo.GetByBatchIdAsync(batchId, cancellationToken);
        var byTeam = results.GroupBy(r => r.TeamId).ToList();
        var aggregates = new List<BatchTeamAggregate>();
        foreach (var g in byTeam)
        {
            var list = g.ToList();
            var teamId = g.Key;
            var teamName = list[0].TeamName;
            var strategyKey = list[0].StrategyKey;
            var points = list.Select(r => r.TotalPoints).ToList();
            var tiles = list.Select(r => r.TilesCompletedCount).ToList();
            var rows = list.Select(r => r.RowReached).ToList();
            var wins = list.Count(r => r.IsWinner);
            var n = list.Count;
            aggregates.Add(new BatchTeamAggregate(
                batchId,
                teamId,
                teamName,
                strategyKey,
                points.Average(),
                points.Min(),
                points.Max(),
                tiles.Average(),
                tiles.Min(),
                tiles.Max(),
                rows.Average(),
                rows.Min(),
                rows.Max(),
                n > 0 ? (double)wins / n : 0,
                n));
        }

        await aggregateRepo.DeleteByBatchIdAsync(batchId, cancellationToken);
        await aggregateRepo.AddRangeAsync(aggregates, cancellationToken);

        batch.SetCompleted(DateTimeOffset.UtcNow);
        await batchRepo.UpdateAsync(batch, cancellationToken);
        var completedDuration = (batch.CompletedAt ?? DateTimeOffset.UtcNow) - batch.CreatedAt;
        metrics?.RecordBatchCompleted(batchId, completedDuration);
        logger.LogInformation("Batch {BatchId} completed", batchId);
    }
}
