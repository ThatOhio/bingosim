namespace BingoSim.Application.DTOs;

/// <summary>
/// Response DTO for a Team (list or detail).
/// </summary>
public record TeamResponse(
    Guid Id,
    Guid EventId,
    string? EventName,
    string Name,
    DateTimeOffset CreatedAt,
    List<Guid> PlayerProfileIds,
    string StrategyKey,
    string? ParamsJson);
