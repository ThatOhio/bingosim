namespace BingoSim.Application.DTOs;

/// <summary>
/// Request DTO for updating an existing PlayerProfile.
/// </summary>
public record UpdatePlayerProfileRequest(
    string Name,
    decimal SkillTimeMultiplier,
    List<CapabilityDto> Capabilities,
    WeeklyScheduleDto WeeklySchedule);
