using BingoSim.Core.ValueObjects;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.ValueObjects;

public class WeeklyScheduleTests
{
    [Fact]
    public void Constructor_Default_CreatesEmptySchedule()
    {
        // Act
        var schedule = new WeeklySchedule();

        // Assert
        schedule.Sessions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithSessions_CreateScheduleWithSessions()
    {
        // Arrange
        var sessions = new[]
        {
            new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120),
            new ScheduledSession(DayOfWeek.Wednesday, new TimeOnly(19, 0), 90)
        };

        // Act
        var schedule = new WeeklySchedule(sessions);

        // Assert
        schedule.Sessions.Should().HaveCount(2);
    }

    [Fact]
    public void AddSession_ValidSession_AddsToSessions()
    {
        // Arrange
        var schedule = new WeeklySchedule();
        var session = new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120);

        // Act
        schedule.AddSession(session);

        // Assert
        schedule.Sessions.Should().ContainSingle()
            .Which.Should().Be(session);
    }

    [Fact]
    public void AddSession_NullSession_ThrowsArgumentNullException()
    {
        // Arrange
        var schedule = new WeeklySchedule();

        // Act
        var act = () => schedule.AddSession(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RemoveSession_ExistingSession_RemovesFromSessions()
    {
        // Arrange
        var session = new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120);
        var schedule = new WeeklySchedule([session]);

        // Act
        schedule.RemoveSession(session);

        // Assert
        schedule.Sessions.Should().BeEmpty();
    }

    [Fact]
    public void ClearSessions_WithSessions_RemovesAllSessions()
    {
        // Arrange
        var schedule = new WeeklySchedule([
            new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120),
            new ScheduledSession(DayOfWeek.Wednesday, new TimeOnly(19, 0), 90)
        ]);

        // Act
        schedule.ClearSessions();

        // Assert
        schedule.Sessions.Should().BeEmpty();
    }

    [Fact]
    public void SetSessions_ReplacesSessions()
    {
        // Arrange
        var schedule = new WeeklySchedule([
            new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120)
        ]);
        var newSessions = new[]
        {
            new ScheduledSession(DayOfWeek.Friday, new TimeOnly(20, 0), 180)
        };

        // Act
        schedule.SetSessions(newSessions);

        // Assert
        schedule.Sessions.Should().HaveCount(1);
        schedule.Sessions[0].DayOfWeek.Should().Be(DayOfWeek.Friday);
    }

    [Fact]
    public void GetSessionsForDay_ReturnsCorrectSessions()
    {
        // Arrange
        var mondaySession1 = new ScheduledSession(DayOfWeek.Monday, new TimeOnly(10, 0), 60);
        var mondaySession2 = new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120);
        var wednesdaySession = new ScheduledSession(DayOfWeek.Wednesday, new TimeOnly(19, 0), 90);

        var schedule = new WeeklySchedule([mondaySession1, mondaySession2, wednesdaySession]);

        // Act
        var mondaySessions = schedule.GetSessionsForDay(DayOfWeek.Monday).ToList();

        // Assert
        mondaySessions.Should().HaveCount(2);
        mondaySessions.Should().Contain(mondaySession1);
        mondaySessions.Should().Contain(mondaySession2);
    }

    [Fact]
    public void TotalWeeklyHours_CalculatesCorrectly()
    {
        // Arrange
        var schedule = new WeeklySchedule([
            new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120),   // 2 hours
            new ScheduledSession(DayOfWeek.Wednesday, new TimeOnly(19, 0), 90), // 1.5 hours
            new ScheduledSession(DayOfWeek.Saturday, new TimeOnly(10, 0), 180)  // 3 hours
        ]);

        // Act
        var totalHours = schedule.TotalWeeklyHours;

        // Assert
        totalHours.Should().Be(6.5);
    }
}
