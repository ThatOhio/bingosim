using BingoSim.Core.Enums;

namespace BingoSim.Application.DTOs;

public sealed class StartSimulationBatchRequest
{
    public required Guid EventId { get; init; }
    public required int RunCount { get; init; }
    public string? Seed { get; init; }
    public required ExecutionMode ExecutionMode { get; init; }
    public string? Name { get; init; }
}
