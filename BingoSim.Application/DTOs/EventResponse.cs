namespace BingoSim.Application.DTOs;

/// <summary>
/// Response DTO for an Event.
/// </summary>
public record EventResponse(
    Guid Id,
    string Name,
    TimeSpan Duration,
    int UnlockPointsRequiredPerRow,
    List<RowDto> Rows,
    DateTimeOffset CreatedAt);
