using BingoSim.Application.Interfaces;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BingoSim.Infrastructure.Services;

/// <summary>
/// Idempotent batch finalization: computes aggregates and marks batch Completed/Error when all runs are terminal.
/// Safe under concurrency with multiple workers.
/// </summary>
public class BatchFinalizationService(
    ISimulationRunRepository runRepo,
    ISimulationBatchRepository batchRepo,
    ITeamRunResultRepository resultRepo,
    IBatchTeamAggregateRepository aggregateRepo,
    ILogger<BatchFinalizationService> logger) : IBatchFinalizationService
{
    public async Task<bool> TryFinalizeAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        if (await runRepo.HasNonTerminalRunsAsync(batchId, cancellationToken))
            return false;

        var runs = await runRepo.GetByBatchIdAsync(batchId, cancellationToken);
        if (runs.Count == 0)
            return false;

        var failedCount = runs.Count(r => r.Status == RunStatus.Failed);
        var completedAt = DateTimeOffset.UtcNow;

        BatchStatus newStatus;
        string? errorMessage;
        if (failedCount > 0)
        {
            newStatus = BatchStatus.Error;
            errorMessage = $"One or more runs failed after 5 attempts ({failedCount} failed).";
        }
        else
        {
            newStatus = BatchStatus.Completed;
            errorMessage = null;
        }

        var won = await batchRepo.TryTransitionToFinalAsync(batchId, newStatus, completedAt, errorMessage, cancellationToken);
        if (!won)
            return false;

        var batch = await batchRepo.GetByIdAsync(batchId, cancellationToken);
        if (batch is null)
            return false;

        var duration = (batch.CompletedAt ?? completedAt) - batch.CreatedAt;
        logger.LogInformation("Batch {BatchId} finalized: {Status} ({FailedCount} failed, duration {Duration}s)",
            batchId, newStatus, failedCount, duration.TotalSeconds);

        if (newStatus == BatchStatus.Error)
            return true;

        var results = await resultRepo.GetByBatchIdAsync(batchId, cancellationToken);
        var byTeam = results.GroupBy(r => r.TeamId).ToList();
        var aggregateList = new List<BatchTeamAggregate>();
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
            aggregateList.Add(new BatchTeamAggregate(
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
        await aggregateRepo.AddRangeAsync(aggregateList, cancellationToken);

        return true;
    }
}
