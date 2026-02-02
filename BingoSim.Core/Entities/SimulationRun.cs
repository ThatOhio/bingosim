using BingoSim.Core.Enums;

namespace BingoSim.Core.Entities;

/// <summary>
/// One simulation run work item within a batch. Supports retries and terminal failure tracking.
/// </summary>
public class SimulationRun
{
    public Guid Id { get; private set; }
    public Guid SimulationBatchId { get; private set; }
    public int RunIndex { get; private set; }
    /// <summary>Derived from batch seed + run index; stored as string for reproducibility and UI display.</summary>
    public string Seed { get; private set; } = string.Empty;
    public RunStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Parameterless constructor for EF Core.</summary>
    private SimulationRun() { }

    public SimulationRun(Guid simulationBatchId, int runIndex, string seed)
    {
        if (simulationBatchId == default)
            throw new ArgumentException("SimulationBatchId cannot be empty.", nameof(simulationBatchId));

        if (runIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(runIndex), "RunIndex cannot be negative.");

        if (string.IsNullOrWhiteSpace(seed))
            throw new ArgumentException("Seed cannot be empty.", nameof(seed));

        Id = Guid.NewGuid();
        SimulationBatchId = simulationBatchId;
        RunIndex = runIndex;
        Seed = seed.Trim();
        Status = RunStatus.Pending;
        AttemptCount = 0;
    }

    public void MarkRunning(DateTimeOffset startedAt)
    {
        Status = RunStatus.Running;
        StartedAt = startedAt;
        LastAttemptAt = startedAt;
    }

    public void MarkCompleted(DateTimeOffset completedAt)
    {
        Status = RunStatus.Completed;
        CompletedAt = completedAt;
    }

    public void MarkFailed(string? lastError, DateTimeOffset attemptedAt)
    {
        LastError = lastError;
        LastAttemptAt = attemptedAt;
        AttemptCount++;
        if (AttemptCount >= 5)
        {
            Status = RunStatus.Failed;
            CompletedAt = attemptedAt;
        }
        else
        {
            Status = RunStatus.Pending; // Allow retry
        }
    }

    public bool IsTerminal => Status == RunStatus.Completed || Status == RunStatus.Failed;
}
