using BingoSim.Application.Simulation.Strategies;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation.Strategies;

public class RowCombinationCalculatorTests
{
    [Fact]
    public void CalculateCombinations_EmptyTiles_ReturnsEmpty()
    {
        var tiles = new Dictionary<string, int>();
        var result = RowCombinationCalculator.CalculateCombinations(tiles, 5);
        result.Should().BeEmpty();
    }

    [Fact]
    public void CalculateCombinations_ThresholdZero_ReturnsEmpty()
    {
        var tiles = new Dictionary<string, int> { ["a"] = 1 };
        var result = RowCombinationCalculator.CalculateCombinations(tiles, 0);
        result.Should().BeEmpty();
    }

    [Fact]
    public void CalculateCombinations_FourTilesThreshold5_FindsValidCombinations()
    {
        var tiles = new Dictionary<string, int>
        {
            ["t1"] = 1,
            ["t2"] = 2,
            ["t3"] = 3,
            ["t4"] = 4
        };
        var result = RowCombinationCalculator.CalculateCombinations(tiles, 5);

        result.Should().NotBeEmpty();
        result.Should().Contain(c => c.TotalPoints >= 5);

        var t1t4 = new[] { "t1", "t4" };
        var t2t3 = new[] { "t2", "t3" };
        var t1t2t3 = new[] { "t1", "t2", "t3" };
        result.Should().Contain(c => c.TileKeys.OrderBy(x => x).SequenceEqual(t1t4));
        result.Should().Contain(c => c.TileKeys.OrderBy(x => x).SequenceEqual(t2t3));
        result.Should().Contain(c => c.TileKeys.OrderBy(x => x).SequenceEqual(t1t2t3));
    }

    [Fact]
    public void CalculateCombinations_AllOnesThreshold5_FindsFiveTileCombination()
    {
        var tiles = new Dictionary<string, int>
        {
            ["a"] = 1,
            ["b"] = 1,
            ["c"] = 1,
            ["d"] = 1,
            ["e"] = 1
        };
        var result = RowCombinationCalculator.CalculateCombinations(tiles, 5);

        result.Should().Contain(c => c.TileKeys.Count == 5 && c.TotalPoints == 5);
    }

    [Fact]
    public void CalculateCombinations_SingleTileMeetsThreshold_ReturnsThatTile()
    {
        var tiles = new Dictionary<string, int>
        {
            ["big"] = 5,
            ["small"] = 1
        };
        var result = RowCombinationCalculator.CalculateCombinations(tiles, 5);

        result.Should().Contain(c => c.TileKeys.Count == 1 && c.TileKeys[0] == "big" && c.TotalPoints == 5);
    }

    [Fact]
    public void CalculateCombinations_NoValidCombination_ReturnsEmpty()
    {
        var tiles = new Dictionary<string, int>
        {
            ["a"] = 1,
            ["b"] = 1,
            ["c"] = 1
        };
        var result = RowCombinationCalculator.CalculateCombinations(tiles, 10);

        result.Should().BeEmpty();
    }

    [Fact]
    public void CalculateCombinations_NoDuplicateCombinations()
    {
        var tiles = new Dictionary<string, int>
        {
            ["t1"] = 1,
            ["t2"] = 2,
            ["t3"] = 3,
            ["t4"] = 4
        };
        var result = RowCombinationCalculator.CalculateCombinations(tiles, 5);

        var sortedKeySets = result
            .Select(c => string.Join(",", c.TileKeys.OrderBy(x => x)))
            .ToList();
        var distinctCount = sortedKeySets.Distinct().Count();
        distinctCount.Should().Be(sortedKeySets.Count, "each combination should be unique");
    }
}
