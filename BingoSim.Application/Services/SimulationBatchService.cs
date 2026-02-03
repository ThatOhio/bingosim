using BingoSim.Application.DTOs;
using BingoSim.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using BingoSim.Application.Simulation;
using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.Exceptions;
using BingoSim.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BingoSim.Application.Services;

/// <summary>
/// Starts simulation batches, creates snapshot and runs; enqueues (local) or publishes (distributed) for execution.
/// </summary>
public class SimulationBatchService(
    IEventRepository eventRepo,
    ITeamRepository teamRepo,
    ISimulationBatchRepository batchRepo,
    IEventSnapshotRepository snapshotRepo,
    ISimulationRunRepository runRepo,
    ITeamRunResultRepository resultRepo,
    IBatchTeamAggregateRepository aggregateRepo,
    EventSnapshotBuilder snapshotBuilder,
    ISimulationRunQueue runQueue,
    [FromKeyedServices("distributed")] ISimulationRunWorkPublisher distributedWorkPublisher,
    IListBatchesQuery listBatchesQuery,
    ILogger<SimulationBatchService> logger) : ISimulationBatchService
{
    public async Task<SimulationBatchResponse> StartBatchAsync(StartSimulationBatchRequest request, CancellationToken cancellationToken = default)
    {
        if (request.RunCount < 1)
            throw new ArgumentOutOfRangeException(nameof(request.RunCount), "Run count must be at least 1.");

        var evt = await eventRepo.GetByIdAsync(request.EventId, cancellationToken);
        if (evt is null)
            throw new EventNotFoundException(request.EventId);

        var teams = await teamRepo.GetByEventIdAsync(request.EventId, cancellationToken);
        if (teams.Count == 0)
            throw new InvalidOperationException($"No teams found for event {request.EventId}. Draft at least one team before running.");

        var seed = string.IsNullOrWhiteSpace(request.Seed)
            ? Guid.NewGuid().ToString("N")
            : request.Seed.Trim();

        var batchName = !string.IsNullOrWhiteSpace(request.Name) ? request.Name.Trim() : evt.Name;
        var batch = new SimulationBatch(
            request.EventId,
            request.RunCount,
            seed,
            request.ExecutionMode,
            batchName);
        await batchRepo.AddAsync(batch, cancellationToken);

        string snapshotJson;
        try
        {
            snapshotJson = await snapshotBuilder.BuildSnapshotJsonAsync(request.EventId, batch.CreatedAt, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            batch.SetError(ex.Message, DateTimeOffset.UtcNow);
            await batchRepo.UpdateAsync(batch, cancellationToken);
            logger.LogWarning("Snapshot build failed for batch {BatchId}: {Message}", batch.Id, ex.Message);
            return ToBatchResponse(batch);
        }

        var snapshotDto = EventSnapshotBuilder.Deserialize(snapshotJson);
        if (snapshotDto is null)
        {
            batch.SetError("Snapshot JSON is invalid or empty.", DateTimeOffset.UtcNow);
            await batchRepo.UpdateAsync(batch, cancellationToken);
            return ToBatchResponse(batch);
        }

        try
        {
            SnapshotValidator.Validate(snapshotDto);
        }
        catch (SnapshotValidationException ex)
        {
            batch.SetError(ex.Message, DateTimeOffset.UtcNow);
            await batchRepo.UpdateAsync(batch, cancellationToken);
            logger.LogWarning("Snapshot validation failed for batch {BatchId}: {Message}", batch.Id, ex.Message);
            return ToBatchResponse(batch);
        }

        var snapshot = new EventSnapshot(batch.Id, snapshotJson);
        await snapshotRepo.AddAsync(snapshot, cancellationToken);

        var runs = new List<SimulationRun>();
        for (var i = 0; i < request.RunCount; i++)
        {
            var runSeedString = SeedDerivation.DeriveRunSeedString(seed, i);
            runs.Add(new SimulationRun(batch.Id, i, runSeedString));
        }
        await runRepo.AddRangeAsync(runs, cancellationToken);

        batch.SetStatus(BatchStatus.Running);
        await batchRepo.UpdateAsync(batch, cancellationToken);

        if (request.ExecutionMode == ExecutionMode.Local)
        {
            foreach (var run in runs)
                await runQueue.EnqueueAsync(run.Id, cancellationToken);
        }
        else
        {
            foreach (var run in runs)
                await distributedWorkPublisher.PublishRunWorkAsync(run.Id, cancellationToken);
        }

        logger.LogInformation("Simulation batch {BatchId} started: {RunCount} runs, seed {Seed}, mode {Mode}",
            batch.Id, request.RunCount, seed, request.ExecutionMode);

        return ToBatchResponse(batch);
    }

    public async Task<SimulationBatchResponse?> GetBatchByIdAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await batchRepo.GetByIdAsync(batchId, cancellationToken);
        return batch is null ? null : ToBatchResponse(batch);
    }

    public Task<ListBatchesResult> GetBatchesAsync(ListBatchesRequest request, CancellationToken cancellationToken = default) =>
        listBatchesQuery.ExecuteAsync(request, cancellationToken);

    public async Task<BatchProgressResponse> GetProgressAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await batchRepo.GetByIdAsync(batchId, cancellationToken);
        var runs = await runRepo.GetByBatchIdAsync(batchId, cancellationToken);
        var completed = runs.Count(r => r.Status == RunStatus.Completed);
        var failed = runs.Count(r => r.Status == RunStatus.Failed);
        var running = runs.Count(r => r.Status == RunStatus.Running);
        var pending = runs.Count(r => r.Status == RunStatus.Pending);
        var retryCount = runs.Sum(r => Math.Max(0, r.AttemptCount - 1));
        var endTime = batch?.CompletedAt ?? DateTimeOffset.UtcNow;
        var startTime = batch?.CreatedAt ?? DateTimeOffset.UtcNow;
        var elapsedSeconds = (endTime - startTime).TotalSeconds;
        var runsPerSecond = elapsedSeconds > 0 ? (completed + failed) / elapsedSeconds : 0.0;
        return new BatchProgressResponse
        {
            Completed = completed,
            Failed = failed,
            Running = running,
            Pending = pending,
            RetryCount = retryCount,
            ElapsedSeconds = Math.Round(elapsedSeconds, 1),
            RunsPerSecond = Math.Round(runsPerSecond, 2)
        };
    }

    public async Task<IReadOnlyList<BatchTeamAggregateResponse>> GetBatchAggregatesAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var aggregates = await aggregateRepo.GetByBatchIdAsync(batchId, cancellationToken);
        return aggregates.Select(a => new BatchTeamAggregateResponse
        {
            TeamId = a.TeamId,
            TeamName = a.TeamName,
            StrategyKey = a.StrategyKey,
            MeanPoints = a.MeanPoints,
            MinPoints = a.MinPoints,
            MaxPoints = a.MaxPoints,
            MeanTilesCompleted = a.MeanTilesCompleted,
            MinTilesCompleted = a.MinTilesCompleted,
            MaxTilesCompleted = a.MaxTilesCompleted,
            MeanRowReached = a.MeanRowReached,
            MinRowReached = a.MinRowReached,
            MaxRowReached = a.MaxRowReached,
            WinnerRate = a.WinnerRate,
            RunCount = a.RunCount
        }).ToList();
    }

    public async Task<IReadOnlyList<TeamRunResultResponse>> GetRunResultsAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var results = await resultRepo.GetByRunIdAsync(runId, cancellationToken);
        return results.Select(r => new TeamRunResultResponse
        {
            SimulationRunId = r.SimulationRunId,
            TeamId = r.TeamId,
            TeamName = r.TeamName,
            StrategyKey = r.StrategyKey,
            StrategyParamsJson = r.StrategyParamsJson,
            TotalPoints = r.TotalPoints,
            TilesCompletedCount = r.TilesCompletedCount,
            RowReached = r.RowReached,
            IsWinner = r.IsWinner,
            RowUnlockTimesJson = r.RowUnlockTimesJson,
            TileCompletionTimesJson = r.TileCompletionTimesJson
        }).ToList();
    }

    public async Task<IReadOnlyList<TeamRunResultResponse>> GetBatchRunResultsForTeamAsync(Guid batchId, Guid teamId, int limit, CancellationToken cancellationToken = default)
    {
        var all = await resultRepo.GetByBatchIdAsync(batchId, cancellationToken);
        var filtered = all.Where(r => r.TeamId == teamId).Take(limit).ToList();
        return filtered.Select(r => new TeamRunResultResponse
        {
            SimulationRunId = r.SimulationRunId,
            TeamId = r.TeamId,
            TeamName = r.TeamName,
            StrategyKey = r.StrategyKey,
            StrategyParamsJson = r.StrategyParamsJson,
            TotalPoints = r.TotalPoints,
            TilesCompletedCount = r.TilesCompletedCount,
            RowReached = r.RowReached,
            IsWinner = r.IsWinner,
            RowUnlockTimesJson = r.RowUnlockTimesJson,
            TileCompletionTimesJson = r.TileCompletionTimesJson
        }).ToList();
    }

    private static SimulationBatchResponse ToBatchResponse(SimulationBatch batch) => new()
    {
        Id = batch.Id,
        EventId = batch.EventId,
        Name = batch.Name,
        RunsRequested = batch.RunsRequested,
        Seed = batch.Seed,
        ExecutionMode = batch.ExecutionMode,
        Status = batch.Status,
        ErrorMessage = batch.ErrorMessage,
        CreatedAt = batch.CreatedAt,
        CompletedAt = batch.CompletedAt
    };
}
