using BingoSim.Application.Simulation.Schedule;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

/// <summary>
/// Unit tests for ScheduleEvaluator pure functions.
/// All timestamps use ET (America/New_York). DayOfWeek: 0=Sunday, 1=Monday, ..., 6=Saturday.
/// </summary>
public class ScheduleEvaluatorTests
{
    private static DateTimeOffset Et(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void IsOnlineAt_EmptySchedule_ReturnsTrue()
    {
        var schedule = new WeeklyScheduleSnapshotDto { Sessions = [] };
        ScheduleEvaluator.IsOnlineAt(schedule, Et(2025, 2, 3, 10, 0)).Should().BeTrue();
    }

    [Fact]
    public void IsOnlineAt_NullSessions_ReturnsTrue()
    {
        var schedule = new WeeklyScheduleSnapshotDto { Sessions = null };
        ScheduleEvaluator.IsOnlineAt(schedule, Et(2025, 2, 3, 10, 0)).Should().BeTrue();
    }

    [Fact]
    public void IsOnlineAt_SingleSession_InsideWindow_ReturnsTrue()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 180 }]
        };
        ScheduleEvaluator.IsOnlineAt(schedule, Et(2025, 2, 3, 10, 0)).Should().BeTrue();
    }

    [Fact]
    public void IsOnlineAt_SingleSession_BeforeWindow_ReturnsFalse()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 180 }]
        };
        ScheduleEvaluator.IsOnlineAt(schedule, Et(2025, 2, 3, 8, 0)).Should().BeFalse();
    }

    [Fact]
    public void IsOnlineAt_SingleSession_AfterWindow_ReturnsFalse()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 180 }]
        };
        ScheduleEvaluator.IsOnlineAt(schedule, Et(2025, 2, 3, 13, 0)).Should().BeFalse();
    }

    [Fact]
    public void IsOnlineAt_SingleSession_AtStart_ReturnsTrue()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 180 }]
        };
        ScheduleEvaluator.IsOnlineAt(schedule, Et(2025, 2, 3, 9, 0)).Should().BeTrue();
    }

    [Fact]
    public void IsOnlineAt_SingleSession_AtEndExclusive_ReturnsFalse()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 180 }]
        };
        ScheduleEvaluator.IsOnlineAt(schedule, Et(2025, 2, 3, 12, 0)).Should().BeFalse();
    }

    [Fact]
    public void IsOnlineAt_MultipleSessionsSameDay_InsideSecond_ReturnsTrue()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions =
            [
                new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 60 },
                new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 14 * 60, DurationMinutes = 120 }
            ]
        };
        ScheduleEvaluator.IsOnlineAt(schedule, Et(2025, 2, 3, 15, 0)).Should().BeTrue();
    }

    [Fact]
    public void IsOnlineAt_MultipleSessionsSameDay_Between_ReturnsFalse()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions =
            [
                new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 60 },
                new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 14 * 60, DurationMinutes = 120 }
            ]
        };
        ScheduleEvaluator.IsOnlineAt(schedule, Et(2025, 2, 3, 12, 0)).Should().BeFalse();
    }

    [Fact]
    public void IsOnlineAt_DifferentDays_MatchesCorrectDay()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 2, StartLocalTimeMinutes = 18 * 60, DurationMinutes = 120 }]
        };
        ScheduleEvaluator.IsOnlineAt(schedule, Et(2025, 2, 4, 19, 0)).Should().BeTrue();
        ScheduleEvaluator.IsOnlineAt(schedule, Et(2025, 2, 3, 19, 0)).Should().BeFalse();
    }

    [Fact]
    public void IsOnlineAt_SessionSpansMidnight_InsideSecondDay_ReturnsTrue()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 23 * 60, DurationMinutes = 120 }]
        };
        ScheduleEvaluator.IsOnlineAt(schedule, Et(2025, 2, 4, 0, 30)).Should().BeTrue();
    }

    [Fact]
    public void IsOnlineAt_SessionSpansMidnight_Outside_ReturnsFalse()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 23 * 60, DurationMinutes = 120 }]
        };
        ScheduleEvaluator.IsOnlineAt(schedule, Et(2025, 2, 4, 1, 30)).Should().BeFalse();
    }

    [Fact]
    public void GetNextSessionStart_EmptySchedule_ReturnsNull()
    {
        var schedule = new WeeklyScheduleSnapshotDto { Sessions = [] };
        ScheduleEvaluator.GetNextSessionStart(schedule, Et(2025, 2, 2, 12, 0)).Should().BeNull();
    }

    [Fact]
    public void GetNextSessionStart_FromBeforeSession_ReturnsSessionStart()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 60 }]
        };
        var next = ScheduleEvaluator.GetNextSessionStart(schedule, Et(2025, 2, 2, 12, 0));
        next.Should().NotBeNull();
        next!.Value.Hour.Should().Be(9);
        next.Value.Day.Should().Be(3);
    }

    [Fact]
    public void GetNextSessionStart_FromInsideSession_ReturnsNextSession()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions =
            [
                new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 60 },
                new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 14 * 60, DurationMinutes = 60 }
            ]
        };
        var next = ScheduleEvaluator.GetNextSessionStart(schedule, Et(2025, 2, 3, 10, 0));
        next.Should().NotBeNull();
        next!.Value.Hour.Should().Be(14);
    }

    [Fact]
    public void GetNextSessionStart_FromExactlyAtSessionStart_ReturnsNextOccurrenceNotSame()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 60 }]
        };
        var fromEt = Et(2025, 2, 3, 9, 0);
        var next = ScheduleEvaluator.GetNextSessionStart(schedule, fromEt);
        next.Should().NotBeNull();
        next!.Value.Should().BeAfter(fromEt);
        next.Value.Day.Should().Be(10);
    }

    [Fact]
    public void GetEarliestNextSessionStart_AllEmptySchedules_ReturnsNull()
    {
        var snapshot = new BingoSim.Application.Simulation.Snapshot.EventSnapshotDto
        {
            EventName = "Test",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = Et(2025, 2, 3, 0, 0).ToString("o"),
            Rows = [],
            ActivitiesById = new Dictionary<Guid, BingoSim.Application.Simulation.Snapshot.ActivitySnapshotDto>(),
            Teams =
            [
                new BingoSim.Application.Simulation.Snapshot.TeamSnapshotDto
                {
                    TeamId = Guid.NewGuid(),
                    TeamName = "T1",
                    StrategyKey = "RowRush",
                    ParamsJson = null,
                    Players =
                    [
                        new BingoSim.Application.Simulation.Snapshot.PlayerSnapshotDto
                        {
                            PlayerId = Guid.NewGuid(),
                            Name = "P1",
                            SkillTimeMultiplier = 1.0m,
                            CapabilityKeys = [],
                            Schedule = new BingoSim.Application.Simulation.Schedule.WeeklyScheduleSnapshotDto { Sessions = [] }
                        }
                    ]
                }
            ]
        };
        var next = ScheduleEvaluator.GetEarliestNextSessionStart(snapshot, Et(2025, 2, 3, 10, 0));
        next.Should().BeNull();
    }

    [Fact]
    public void GetCurrentSessionEnd_Offline_ReturnsNull()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 180 }]
        };
        ScheduleEvaluator.GetCurrentSessionEnd(schedule, Et(2025, 2, 3, 8, 0)).Should().BeNull();
    }

    [Fact]
    public void GetCurrentSessionEnd_Online_ReturnsSessionEnd()
    {
        var schedule = new WeeklyScheduleSnapshotDto
        {
            Sessions = [new ScheduledSessionSnapshotDto { DayOfWeek = 1, StartLocalTimeMinutes = 9 * 60, DurationMinutes = 180 }]
        };
        var end = ScheduleEvaluator.GetCurrentSessionEnd(schedule, Et(2025, 2, 3, 10, 0));
        end.Should().NotBeNull();
        end!.Value.Hour.Should().Be(12);
        end.Value.Minute.Should().Be(0);
    }

    [Fact]
    public void SimTimeToEt_AddsSeconds()
    {
        var start = Et(2025, 2, 3, 0, 0);
        var result = ScheduleEvaluator.SimTimeToEt(start, 3600);
        result.Hour.Should().Be(1);
        result.Day.Should().Be(3);
    }

    [Fact]
    public void EtToSimTime_ReturnsSeconds()
    {
        var start = Et(2025, 2, 3, 0, 0);
        var end = Et(2025, 2, 3, 1, 0);
        ScheduleEvaluator.EtToSimTime(start, end).Should().Be(3600);
    }
}
