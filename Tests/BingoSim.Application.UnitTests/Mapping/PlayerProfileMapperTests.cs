using BingoSim.Application.DTOs;
using BingoSim.Application.Mapping;
using BingoSim.Core.Entities;
using BingoSim.Core.ValueObjects;
using FluentAssertions;

namespace BingoSim.Application.UnitTests.Mapping;

public class PlayerProfileMapperTests
{
    [Fact]
    public void ToResponse_MapsAllFields()
    {
        // Arrange
        var profile = new PlayerProfile("TestPlayer", 0.8m);
        profile.AddCapability(new Capability("quest.ds2", "Desert Treasure 2"));
        profile.SetWeeklySchedule(new WeeklySchedule([
            new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120)
        ]));

        // Act
        var response = PlayerProfileMapper.ToResponse(profile);

        // Assert
        response.Id.Should().Be(profile.Id);
        response.Name.Should().Be("TestPlayer");
        response.SkillTimeMultiplier.Should().Be(0.8m);
        response.Capabilities.Should().HaveCount(1);
        response.Capabilities[0].Key.Should().Be("quest.ds2");
        response.WeeklySchedule.Sessions.Should().HaveCount(1);
        response.WeeklySchedule.Sessions[0].DayOfWeek.Should().Be(DayOfWeek.Monday);
        response.CreatedAt.Should().Be(profile.CreatedAt);
    }

    [Fact]
    public void ToDto_Capability_MapsCorrectly()
    {
        // Arrange
        var capability = new Capability("quest.ds2", "Desert Treasure 2");

        // Act
        var dto = PlayerProfileMapper.ToDto(capability);

        // Assert
        dto.Key.Should().Be("quest.ds2");
        dto.Name.Should().Be("Desert Treasure 2");
    }

    [Fact]
    public void ToDto_ScheduledSession_MapsCorrectly()
    {
        // Arrange
        var session = new ScheduledSession(DayOfWeek.Friday, new TimeOnly(20, 30), 90);

        // Act
        var dto = PlayerProfileMapper.ToDto(session);

        // Assert
        dto.DayOfWeek.Should().Be(DayOfWeek.Friday);
        dto.StartTime.Should().Be(new TimeOnly(20, 30));
        dto.DurationMinutes.Should().Be(90);
    }

    [Fact]
    public void ToDto_WeeklySchedule_MapsAllSessions()
    {
        // Arrange
        var schedule = new WeeklySchedule([
            new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120),
            new ScheduledSession(DayOfWeek.Wednesday, new TimeOnly(19, 0), 90)
        ]);

        // Act
        var dto = PlayerProfileMapper.ToDto(schedule);

        // Assert
        dto.Sessions.Should().HaveCount(2);
    }

    [Fact]
    public void ToEntity_CapabilityDto_MapsCorrectly()
    {
        // Arrange
        var dto = new CapabilityDto("quest.ds2", "Desert Treasure 2");

        // Act
        var entity = PlayerProfileMapper.ToEntity(dto);

        // Assert
        entity.Key.Should().Be("quest.ds2");
        entity.Name.Should().Be("Desert Treasure 2");
    }

    [Fact]
    public void ToEntity_ScheduledSessionDto_MapsCorrectly()
    {
        // Arrange
        var dto = new ScheduledSessionDto(DayOfWeek.Friday, new TimeOnly(20, 30), 90);

        // Act
        var entity = PlayerProfileMapper.ToEntity(dto);

        // Assert
        entity.DayOfWeek.Should().Be(DayOfWeek.Friday);
        entity.StartLocalTime.Should().Be(new TimeOnly(20, 30));
        entity.DurationMinutes.Should().Be(90);
    }

    [Fact]
    public void ToEntity_WeeklyScheduleDto_MapsAllSessions()
    {
        // Arrange
        var dto = new WeeklyScheduleDto([
            new ScheduledSessionDto(DayOfWeek.Monday, new TimeOnly(18, 0), 120),
            new ScheduledSessionDto(DayOfWeek.Wednesday, new TimeOnly(19, 0), 90)
        ]);

        // Act
        var entity = PlayerProfileMapper.ToEntity(dto);

        // Assert
        entity.Sessions.Should().HaveCount(2);
        entity.Sessions[0].DayOfWeek.Should().Be(DayOfWeek.Monday);
        entity.Sessions[1].DayOfWeek.Should().Be(DayOfWeek.Wednesday);
    }
}
