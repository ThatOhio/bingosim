using BingoSim.Core.ValueObjects;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.ValueObjects;

public class GroupSizeBandTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesValueObject()
    {
        var vo = new GroupSizeBand(2, 4, 0.9m, 1.1m);
        vo.MinSize.Should().Be(2);
        vo.MaxSize.Should().Be(4);
        vo.TimeMultiplier.Should().Be(0.9m);
        vo.ProbabilityMultiplier.Should().Be(1.1m);
    }

    [Fact]
    public void Constructor_MinSizeLessThanOne_Throws()
    {
        var act = () => new GroupSizeBand(0, 4, 1.0m, 1.0m);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("minSize");
    }

    [Fact]
    public void Constructor_MaxLessThanMin_Throws()
    {
        var act = () => new GroupSizeBand(5, 3, 1.0m, 1.0m);
        act.Should().Throw<ArgumentException>().WithParameterName("maxSize");
    }

    [Fact]
    public void Constructor_ZeroTimeMultiplier_Throws()
    {
        var act = () => new GroupSizeBand(1, 4, 0m, 1.0m);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("timeMultiplier");
    }

    [Fact]
    public void Constructor_ZeroProbabilityMultiplier_Throws()
    {
        var act = () => new GroupSizeBand(1, 4, 1.0m, 0m);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("probabilityMultiplier");
    }
}
