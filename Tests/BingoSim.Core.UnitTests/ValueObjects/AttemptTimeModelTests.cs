using BingoSim.Core.Enums;
using BingoSim.Core.ValueObjects;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.ValueObjects;

public class AttemptTimeModelTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesValueObject()
    {
        var vo = new AttemptTimeModel(120, TimeDistribution.NormalApprox, 15);
        vo.BaselineTimeSeconds.Should().Be(120);
        vo.Distribution.Should().Be(TimeDistribution.NormalApprox);
        vo.VarianceSeconds.Should().Be(15);
    }

    [Fact]
    public void Constructor_ZeroBaseline_Throws()
    {
        var act = () => new AttemptTimeModel(0, TimeDistribution.Uniform);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("baselineTimeSeconds");
    }

    [Fact]
    public void Constructor_NegativeVariance_Throws()
    {
        var act = () => new AttemptTimeModel(60, TimeDistribution.Uniform, -1);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("varianceSeconds");
    }
}
