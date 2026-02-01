using BingoSim.Core.Entities;
using BingoSim.Core.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BingoSim.Core.UnitTests.Entities;

public class EventTests
{
    private static TileActivityRule MinimalRule() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "activity.key",
        [],
        [],
        []);

    private static Tile Tile(int points, string key) => new(key, $"Tile {points}", points, 1, [MinimalRule()]);

    private static Row Row(int index, string keyPrefix) => new(index, [
        Tile(1, $"{keyPrefix}.p1"),
        Tile(2, $"{keyPrefix}.p2"),
        Tile(3, $"{keyPrefix}.p3"),
        Tile(4, $"{keyPrefix}.p4")
    ]);

    [Fact]
    public void Constructor_ValidParameters_CreatesEvent()
    {
        var evt = new Event("Winter Bingo", TimeSpan.FromHours(24), 5);

        evt.Id.Should().NotBe(Guid.Empty);
        evt.Name.Should().Be("Winter Bingo");
        evt.Duration.Should().Be(TimeSpan.FromHours(24));
        evt.UnlockPointsRequiredPerRow.Should().Be(5);
        evt.Rows.Should().BeEmpty();
        evt.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyName_ThrowsArgumentException(string? name)
    {
        var act = () => new Event(name!, TimeSpan.FromHours(1), 5);
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Constructor_ZeroDuration_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new Event("Name", TimeSpan.Zero, 5);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("duration");
    }

    [Fact]
    public void Constructor_NegativeUnlockPoints_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new Event("Name", TimeSpan.FromHours(1), -1);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("unlockPointsRequiredPerRow");
    }

    [Fact]
    public void SetRows_ValidRows_SetsRowsOrderedByIndex()
    {
        var evt = new Event("E", TimeSpan.FromHours(1), 5);
        var rows = new[] { Row(1, "r1"), Row(0, "r0") };

        evt.SetRows(rows);

        evt.Rows.Should().HaveCount(2);
        evt.Rows[0].Index.Should().Be(0);
        evt.Rows[1].Index.Should().Be(1);
    }

    [Fact]
    public void SetRows_DuplicateTileKeyAcrossEvent_ThrowsInvalidOperationException()
    {
        var evt = new Event("E", TimeSpan.FromHours(1), 5);
        var row0 = Row(0, "r0");
        var row1 = new Row(1, [
            Tile(1, "r0.p1"),
            Tile(2, "r1.p2"),
            Tile(3, "r1.p3"),
            Tile(4, "r1.p4")
        ]);

        var act = () => evt.SetRows([row0, row1]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*unique*");
    }

    [Fact]
    public void UpdateName_ValidName_Updates()
    {
        var evt = new Event("Old", TimeSpan.FromHours(1), 5);
        evt.SetRows([Row(0, "r0")]);

        evt.UpdateName("New Name");

        evt.Name.Should().Be("New Name");
    }

    [Fact]
    public void UpdateDuration_ValidDuration_Updates()
    {
        var evt = new Event("E", TimeSpan.FromHours(1), 5);
        evt.SetRows([Row(0, "r0")]);

        evt.UpdateDuration(TimeSpan.FromHours(48));

        evt.Duration.Should().Be(TimeSpan.FromHours(48));
    }

    [Fact]
    public void SetUnlockPointsRequiredPerRow_ValidValue_Updates()
    {
        var evt = new Event("E", TimeSpan.FromHours(1), 5);
        evt.SetRows([Row(0, "r0")]);

        evt.SetUnlockPointsRequiredPerRow(10);

        evt.UnlockPointsRequiredPerRow.Should().Be(10);
    }
}
