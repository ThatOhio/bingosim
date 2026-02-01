namespace BingoSim.Application.DTOs;

/// <summary>
/// DTO for a weekly schedule containing multiple sessions.
/// </summary>
public record WeeklyScheduleDto(List<ScheduledSessionDto> Sessions);
