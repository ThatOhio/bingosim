using BingoSim.Core.Enums;

namespace BingoSim.Application.DTOs;

public sealed class SimulationBatchResponse
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int RunsRequested { get; init; }
    public string Seed { get; init; } = string.Empty;
    public ExecutionMode ExecutionMode { get; init; }
    public BatchStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}
