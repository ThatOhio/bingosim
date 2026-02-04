using BingoSim.Application.Simulation.Allocation;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

public class GreedyPointsAllocatorTests
{
    private readonly GreedyPointsAllocator _sut = new();

    [Fact]
    public void SelectTargetTileForGrant_NoEligible_ReturnsNull()
    {
        var context = new GrantAllocationContext
        {
            UnlockedRowIndices = new HashSet<int> { 0 },
            TileProgress = new Dictionary<string, int>(),
            TileRequiredCount = new Dictionary<string, int>(),
            TileRowIndex = new Dictionary<string, int>(),
            TilePoints = new Dictionary<string, int>(),
            EligibleTileKeys = []
        };
        _sut.SelectTargetTileForGrant(context).Should().BeNull();
    }

    [Fact]
    public void SelectTargetTileForGrant_PrefersHighestPointsThenLowestRow()
    {
        var context = new GrantAllocationContext
        {
            UnlockedRowIndices = new HashSet<int> { 0, 1 },
            TileProgress = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0, ["c"] = 0 },
            TileRequiredCount = new Dictionary<string, int> { ["a"] = 1, ["b"] = 1, ["c"] = 1 },
            TileRowIndex = new Dictionary<string, int> { ["a"] = 0, ["b"] = 1, ["c"] = 0 },
            TilePoints = new Dictionary<string, int> { ["a"] = 2, ["b"] = 4, ["c"] = 4 },
            EligibleTileKeys = ["a", "b", "c"]
        };
        _sut.SelectTargetTileForGrant(context).Should().Be("c");
    }

    [Fact]
    public void SelectTargetTileForGrant_TieBreakByTileKey()
    {
        var context = new GrantAllocationContext
        {
            UnlockedRowIndices = new HashSet<int> { 0 },
            TileProgress = new Dictionary<string, int> { ["x"] = 0, ["a"] = 0 },
            TileRequiredCount = new Dictionary<string, int> { ["x"] = 1, ["a"] = 1 },
            TileRowIndex = new Dictionary<string, int> { ["x"] = 0, ["a"] = 0 },
            TilePoints = new Dictionary<string, int> { ["x"] = 4, ["a"] = 4 },
            EligibleTileKeys = ["x", "a"]
        };
        _sut.SelectTargetTileForGrant(context).Should().Be("a");
    }
}
