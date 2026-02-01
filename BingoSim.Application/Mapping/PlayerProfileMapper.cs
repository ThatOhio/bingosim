using BingoSim.Application.DTOs;
using BingoSim.Core.Entities;
using BingoSim.Core.ValueObjects;

namespace BingoSim.Application.Mapping;

/// <summary>
/// Manual mapping between PlayerProfile entities and DTOs.
/// </summary>
public static class PlayerProfileMapper
{
    public static PlayerProfileResponse ToResponse(PlayerProfile entity)
    {
        return new PlayerProfileResponse(
            Id: entity.Id,
            Name: entity.Name,
            SkillTimeMultiplier: entity.SkillTimeMultiplier,
            Capabilities: entity.Capabilities.Select(ToDto).ToList(),
            WeeklySchedule: ToDto(entity.WeeklySchedule),
            CreatedAt: entity.CreatedAt);
    }

    public static CapabilityDto ToDto(Capability capability)
    {
        return new CapabilityDto(capability.Key, capability.Name);
    }

    public static WeeklyScheduleDto ToDto(WeeklySchedule schedule)
    {
        return new WeeklyScheduleDto(
            Sessions: schedule.Sessions.Select(ToDto).ToList());
    }

    public static ScheduledSessionDto ToDto(ScheduledSession session)
    {
        return new ScheduledSessionDto(
            DayOfWeek: session.DayOfWeek,
            StartTime: session.StartLocalTime,
            DurationMinutes: session.DurationMinutes);
    }

    public static Capability ToEntity(CapabilityDto dto)
    {
        return new Capability(dto.Key, dto.Name);
    }

    public static WeeklySchedule ToEntity(WeeklyScheduleDto dto)
    {
        var sessions = dto.Sessions.Select(ToEntity);
        return new WeeklySchedule(sessions);
    }

    public static ScheduledSession ToEntity(ScheduledSessionDto dto)
    {
        return new ScheduledSession(dto.DayOfWeek, dto.StartTime, dto.DurationMinutes);
    }
}
