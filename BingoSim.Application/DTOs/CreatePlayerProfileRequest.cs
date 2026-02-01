namespace BingoSim.Application.DTOs;

/// <summary>
/// Request DTO for creating a new PlayerProfile.
/// </summary>
public record CreatePlayerProfileRequest(
    string Name,
    decimal SkillTimeMultiplier,
    List<CapabilityDto> Capabilities,
    WeeklyScheduleDto WeeklySchedule);
