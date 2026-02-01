using BingoSim.Core.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BingoSim.Core.UnitTests.ValueObjects;

public class RowTests
{
    private static TileActivityRule MinimalRule() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "activity.key",
        [],
        [],
        []);

    private static Tile Tile(int points, string key) => new(key, $"Tile {points}", points, 1, [MinimalRule()]);

    [Fact]
    public void Constructor_FourTilesWithPoints1234_CreatesRow()
    {
        var tiles = new[]
        {
            Tile(1, "r0.p1"),
            Tile(2, "r0.p2"),
            Tile(3, "r0.p3"),
            Tile(4, "r0.p4")
        };
        var row = new Row(0, tiles);

        row.Index.Should().Be(0);
        row.Tiles.Should().HaveCount(4);
        row.Tiles.Select(t => t.Points).OrderBy(p => p).Should().BeEquivalentTo([1, 2, 3, 4]);
    }

    [Fact]
    public void Constructor_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        var tiles = new[] { Tile(1, "p1"), Tile(2, "p2"), Tile(3, "p3"), Tile(4, "p4") };
        var act = () => new Row(-1, tiles);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("index");
    }

    [Fact]
    public void Constructor_ThreeTiles_ThrowsArgumentException()
    {
        var tiles = new[] { Tile(1, "p1"), Tile(2, "p2"), Tile(3, "p3") };
        var act = () => new Row(0, tiles);
        act.Should().Throw<ArgumentException>().WithMessage("*exactly 4*");
    }

    [Fact]
    public void Constructor_FourTilesWrongPoints_ThrowsArgumentException()
    {
        var tiles = new[] { Tile(1, "p1"), Tile(1, "p2"), Tile(2, "p3"), Tile(3, "p4") };
        var act = () => new Row(0, tiles);
        act.Should().Throw<ArgumentException>().WithMessage("*1, 2, 3, 4*");
    }
}
