using BingoSim.Application.Simulation.Runner;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

public class RowUnlockHelperTests
{
    [Fact]
    public void ComputeUnlockedRows_Row0AlwaysUnlocked()
    {
        var unlocked = RowUnlockHelper.ComputeUnlockedRows(5, new Dictionary<int, int>(), 3);
        unlocked.Should().Contain(0);
        unlocked.Should().HaveCount(1);
    }

    [Fact]
    public void ComputeUnlockedRows_Row1UnlocksWhenRow0Has5Points()
    {
        var completed = new Dictionary<int, int> { [0] = 5 };
        var unlocked = RowUnlockHelper.ComputeUnlockedRows(5, completed, 3);
        unlocked.Should().Contain(0);
        unlocked.Should().Contain(1);
        unlocked.Should().HaveCount(2);
    }

    [Fact]
    public void ComputeUnlockedRows_Row1StaysLockedWhenRow0Has4Points()
    {
        var completed = new Dictionary<int, int> { [0] = 4 };
        var unlocked = RowUnlockHelper.ComputeUnlockedRows(5, completed, 3);
        unlocked.Should().Contain(0);
        unlocked.Should().NotContain(1);
        unlocked.Should().HaveCount(1);
    }

    [Fact]
    public void ComputeUnlockedRows_Row2UnlocksWhenRow1Has5Points()
    {
        var completed = new Dictionary<int, int> { [0] = 10, [1] = 5 };
        var unlocked = RowUnlockHelper.ComputeUnlockedRows(5, completed, 3);
        unlocked.Should().Contain(0);
        unlocked.Should().Contain(1);
        unlocked.Should().Contain(2);
        unlocked.Should().HaveCount(3);
    }
}
