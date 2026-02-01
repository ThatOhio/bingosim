using BingoSim.Core.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BingoSim.Core.UnitTests.ValueObjects;

public class TileTests
{
    private static TileActivityRule MinimalRule() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "activity.key",
        [],
        [],
        []);

    [Fact]
    public void Constructor_ValidParameters_CreatesTile()
    {
        var rule = MinimalRule();
        var tile = new Tile("tile.r1.p1", "Tile 1", 1, 1, [rule]);

        tile.Key.Should().Be("tile.r1.p1");
        tile.Name.Should().Be("Tile 1");
        tile.Points.Should().Be(1);
        tile.RequiredCount.Should().Be(1);
        tile.AllowedActivities.Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyKey_ThrowsArgumentException(string? key)
    {
        var act = () => new Tile(key!, "Name", 1, 1, [MinimalRule()]);
        act.Should().Throw<ArgumentException>().WithParameterName("key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyName_ThrowsArgumentException(string? name)
    {
        var act = () => new Tile("key", name!, 1, 1, [MinimalRule()]);
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Constructor_PointsOutOfRange_ThrowsArgumentOutOfRangeException(int points)
    {
        var act = () => new Tile("key", "Name", points, 1, [MinimalRule()]);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("points");
    }

    [Fact]
    public void Constructor_RequiredCountZero_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new Tile("key", "Name", 1, 0, [MinimalRule()]);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("requiredCount");
    }

    [Fact]
    public void Constructor_EmptyAllowedActivities_ThrowsArgumentException()
    {
        var act = () => new Tile("key", "Name", 1, 1, []);
        act.Should().Throw<ArgumentException>().WithParameterName("allowedActivities");
    }
}
