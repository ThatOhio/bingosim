namespace BingoSim.Application.DTOs;

/// <summary>
/// DTO for a scheduled play session.
/// </summary>
public record ScheduledSessionDto(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    int DurationMinutes);
