namespace BingoSim.Application.DTOs;

/// <summary>
/// DTO for a tile (event-specific goal with points 1-4).
/// </summary>
public record TileDto(string Key, string Name, int Points, int RequiredCount, List<TileActivityRuleDto> AllowedActivities);
