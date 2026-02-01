namespace BingoSim.Application.DTOs;

/// <summary>
/// Response DTO for a PlayerProfile.
/// </summary>
public record PlayerProfileResponse(
    Guid Id,
    string Name,
    decimal SkillTimeMultiplier,
    List<CapabilityDto> Capabilities,
    WeeklyScheduleDto WeeklySchedule,
    DateTimeOffset CreatedAt);
