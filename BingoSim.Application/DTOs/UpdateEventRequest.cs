namespace BingoSim.Application.DTOs;

/// <summary>
/// Request DTO for updating an existing Event.
/// </summary>
public record UpdateEventRequest(
    string Name,
    TimeSpan Duration,
    int UnlockPointsRequiredPerRow,
    List<RowDto> Rows);
