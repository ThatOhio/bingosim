using BingoSim.Core.Enums;

namespace BingoSim.Core.Entities;

/// <summary>
/// Represents one user-initiated batch of simulation runs.
/// </summary>
public class SimulationBatch
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int RunsRequested { get; private set; }
    /// <summary>User-provided or system-generated seed; stored as string for reproducibility and UI display.</summary>
    public string Seed { get; private set; } = string.Empty;
    public ExecutionMode ExecutionMode { get; private set; }
    public BatchStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Parameterless constructor for EF Core.</summary>
    private SimulationBatch() { }

    public SimulationBatch(
        Guid eventId,
        int runsRequested,
        string seed,
        ExecutionMode executionMode,
        string? name = null)
    {
        if (eventId == default)
            throw new ArgumentException("EventId cannot be empty.", nameof(eventId));

        if (runsRequested < 1)
            throw new ArgumentOutOfRangeException(nameof(runsRequested), "RunsRequested must be at least 1.");

        if (string.IsNullOrWhiteSpace(seed))
            throw new ArgumentException("Seed cannot be empty.", nameof(seed));

        Id = Guid.NewGuid();
        EventId = eventId;
        Name = name?.Trim() ?? string.Empty;
        RunsRequested = runsRequested;
        Seed = seed.Trim();
        ExecutionMode = executionMode;
        Status = BatchStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetStatus(BatchStatus status)
    {
        Status = status;
    }

    public void SetCompleted(DateTimeOffset completedAt)
    {
        Status = BatchStatus.Completed;
        CompletedAt = completedAt;
    }

    public void SetError(string? errorMessage, DateTimeOffset completedAt)
    {
        Status = BatchStatus.Error;
        ErrorMessage = errorMessage;
        CompletedAt = completedAt;
    }
}
