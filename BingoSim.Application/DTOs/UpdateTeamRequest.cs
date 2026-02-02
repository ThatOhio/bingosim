namespace BingoSim.Application.DTOs;

/// <summary>
/// Request DTO for updating a Team (name, roster, strategy).
/// </summary>
public record UpdateTeamRequest(
    string Name,
    List<Guid> PlayerProfileIds,
    string StrategyKey,
    string? ParamsJson);
