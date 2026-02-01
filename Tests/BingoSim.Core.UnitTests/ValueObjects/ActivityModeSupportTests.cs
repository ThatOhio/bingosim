using BingoSim.Core.ValueObjects;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.ValueObjects;

public class ActivityModeSupportTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesValueObject()
    {
        var vo = new ActivityModeSupport(true, true, 2, 8);
        vo.SupportsSolo.Should().BeTrue();
        vo.SupportsGroup.Should().BeTrue();
        vo.MinGroupSize.Should().Be(2);
        vo.MaxGroupSize.Should().Be(8);
    }

    [Fact]
    public void Constructor_MinGroupSizeLessThanOne_Throws()
    {
        var act = () => new ActivityModeSupport(true, true, 0, 4);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("minGroupSize");
    }

    [Fact]
    public void Constructor_MaxGroupSizeLessThanOne_Throws()
    {
        var act = () => new ActivityModeSupport(true, true, 2, 0);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxGroupSize");
    }

    [Fact]
    public void Constructor_MinGreaterThanMax_Throws()
    {
        var act = () => new ActivityModeSupport(true, true, 5, 3);
        act.Should().Throw<ArgumentException>().WithParameterName("minGroupSize");
    }
}
