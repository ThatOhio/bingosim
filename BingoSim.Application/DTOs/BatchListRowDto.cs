using BingoSim.Core.Enums;

namespace BingoSim.Application.DTOs;

/// <summary>
/// One row in the simulation batches list (results discovery).
/// </summary>
public sealed class BatchListRowDto
{
    public Guid BatchId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public BatchStatus Status { get; init; }
    public string EventName { get; init; } = string.Empty;
    public int RunCount { get; init; }
    public int CompletedCount { get; init; }
    public int FailedCount { get; init; }
    public string Seed { get; init; } = string.Empty;
    public ExecutionMode? ExecutionMode { get; init; }
}
