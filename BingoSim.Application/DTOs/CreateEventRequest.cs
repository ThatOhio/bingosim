namespace BingoSim.Application.DTOs;

/// <summary>
/// Request DTO for creating a new Event.
/// </summary>
public record CreateEventRequest(
    string Name,
    TimeSpan Duration,
    int UnlockPointsRequiredPerRow,
    List<RowDto> Rows);
