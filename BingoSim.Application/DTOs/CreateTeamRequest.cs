namespace BingoSim.Application.DTOs;

/// <summary>
/// Request DTO for creating a new Team.
/// </summary>
public record CreateTeamRequest(
    Guid EventId,
    string Name,
    List<Guid> PlayerProfileIds,
    string StrategyKey,
    string? ParamsJson);
