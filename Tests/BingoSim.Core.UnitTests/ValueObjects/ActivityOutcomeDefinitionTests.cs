using BingoSim.Core.ValueObjects;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.ValueObjects;

public class ActivityOutcomeDefinitionTests
{
    private static ProgressGrant DefaultGrant => new("drop.key", 1);

    [Fact]
    public void Constructor_ValidParameters_CreatesValueObject()
    {
        var vo = new ActivityOutcomeDefinition("common", 1, 2, [DefaultGrant]);
        vo.Key.Should().Be("common");
        vo.WeightNumerator.Should().Be(1);
        vo.WeightDenominator.Should().Be(2);
        vo.Grants.Should().HaveCount(1);
        vo.Grants[0].DropKey.Should().Be("drop.key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyKey_Throws(string? key)
    {
        var act = () => new ActivityOutcomeDefinition(key!, 1, 1, [DefaultGrant]);
        act.Should().Throw<ArgumentException>().WithParameterName("key");
    }

    [Fact]
    public void Constructor_ZeroWeightNumerator_Throws()
    {
        var act = () => new ActivityOutcomeDefinition("key", 0, 1, [DefaultGrant]);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("weightNumerator");
    }

    [Fact]
    public void Constructor_ZeroWeightDenominator_Throws()
    {
        var act = () => new ActivityOutcomeDefinition("key", 1, 0, [DefaultGrant]);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("weightDenominator");
    }
}
