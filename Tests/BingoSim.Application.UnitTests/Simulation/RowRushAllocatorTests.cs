using BingoSim.Application.Simulation.Allocation;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

public class RowRushAllocatorTests
{
    private readonly RowRushAllocator _sut = new();

    [Fact]
    public void SelectTargetTile_NoEligible_ReturnsNull()
    {
        var context = new AllocatorContext
        {
            UnlockedRowIndices = new HashSet<int> { 0 },
            TileProgress = new Dictionary<string, int>(),
            TileRequiredCount = new Dictionary<string, int>(),
            TileRowIndex = new Dictionary<string, int>(),
            TilePoints = new Dictionary<string, int>(),
            EligibleTileKeys = []
        };
        _sut.SelectTargetTile(context).Should().BeNull();
    }

    [Fact]
    public void SelectTargetTile_PrefersLowestRowThenLowestPoints()
    {
        var context = new AllocatorContext
        {
            UnlockedRowIndices = new HashSet<int> { 0, 1 },
            TileProgress = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0, ["c"] = 0 },
            TileRequiredCount = new Dictionary<string, int> { ["a"] = 1, ["b"] = 1, ["c"] = 1 },
            TileRowIndex = new Dictionary<string, int> { ["a"] = 1, ["b"] = 0, ["c"] = 0 },
            TilePoints = new Dictionary<string, int> { ["a"] = 1, ["b"] = 4, ["c"] = 1 },
            EligibleTileKeys = ["a", "b", "c"]
        };
        _sut.SelectTargetTile(context).Should().Be("c");
    }

    [Fact]
    public void SelectTargetTile_TieBreakByTileKey()
    {
        var context = new AllocatorContext
        {
            UnlockedRowIndices = new HashSet<int> { 0 },
            TileProgress = new Dictionary<string, int> { ["x"] = 0, ["a"] = 0 },
            TileRequiredCount = new Dictionary<string, int> { ["x"] = 1, ["a"] = 1 },
            TileRowIndex = new Dictionary<string, int> { ["x"] = 0, ["a"] = 0 },
            TilePoints = new Dictionary<string, int> { ["x"] = 1, ["a"] = 1 },
            EligibleTileKeys = ["x", "a"]
        };
        _sut.SelectTargetTile(context).Should().Be("a");
    }
}
