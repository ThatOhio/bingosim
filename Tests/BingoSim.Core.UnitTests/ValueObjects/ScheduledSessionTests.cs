using BingoSim.Core.ValueObjects;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.ValueObjects;

public class ScheduledSessionTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesSession()
    {
        // Arrange
        var day = DayOfWeek.Monday;
        var startTime = new TimeOnly(18, 0);
        var duration = 120;

        // Act
        var session = new ScheduledSession(day, startTime, duration);

        // Assert
        session.DayOfWeek.Should().Be(day);
        session.StartLocalTime.Should().Be(startTime);
        session.DurationMinutes.Should().Be(duration);
    }

    [Fact]
    public void Constructor_ZeroDuration_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act
        var act = () => new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("durationMinutes");
    }

    [Fact]
    public void Constructor_NegativeDuration_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act
        var act = () => new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), -60);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("durationMinutes");
    }

    [Fact]
    public void EndLocalTime_ReturnsCorrectEndTime()
    {
        // Arrange
        var session = new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120);

        // Act
        var endTime = session.EndLocalTime;

        // Assert
        endTime.Should().Be(new TimeOnly(20, 0));
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var session1 = new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120);
        var session2 = new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120);

        // Assert
        session1.Should().Be(session2);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var session1 = new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120);
        var session2 = new ScheduledSession(DayOfWeek.Tuesday, new TimeOnly(18, 0), 120);

        // Assert
        session1.Should().NotBe(session2);
    }
}
