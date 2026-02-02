using BingoSim.Application.Simulation;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

public class SeedDerivationTests
{
    [Fact]
    public void DeriveRngSeed_SameInputs_ReturnsSameValue()
    {
        var a = SeedDerivation.DeriveRngSeed("abc", 0);
        var b = SeedDerivation.DeriveRngSeed("abc", 0);
        a.Should().Be(b);
    }

    [Fact]
    public void DeriveRngSeed_DifferentRunIndex_ReturnsDifferentValue()
    {
        var a = SeedDerivation.DeriveRngSeed("abc", 0);
        var b = SeedDerivation.DeriveRngSeed("abc", 1);
        a.Should().NotBe(b);
    }

    [Fact]
    public void DeriveRunSeedString_FormatCorrect()
    {
        SeedDerivation.DeriveRunSeedString("myseed", 3).Should().Be("myseed_3");
    }
}
