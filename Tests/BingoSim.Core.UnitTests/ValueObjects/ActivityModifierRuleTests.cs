using BingoSim.Core.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BingoSim.Core.UnitTests.ValueObjects;

public class ActivityModifierRuleTests
{
    private static Capability Cap() => new("cap.key", "Cap Name");

    [Fact]
    public void Constructor_WithTimeMultiplier_CreatesRule()
    {
        var cap = Cap();
        var rule = new ActivityModifierRule(cap, 0.8m, null);

        rule.Capability.Should().Be(cap);
        rule.TimeMultiplier.Should().Be(0.8m);
        rule.ProbabilityMultiplier.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithProbabilityMultiplier_CreatesRule()
    {
        var cap = Cap();
        var rule = new ActivityModifierRule(cap, null, 1.2m);

        rule.ProbabilityMultiplier.Should().Be(1.2m);
        rule.TimeMultiplier.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithBothMultipliers_CreatesRule()
    {
        var cap = Cap();
        var rule = new ActivityModifierRule(cap, 0.9m, 1.1m);

        rule.TimeMultiplier.Should().Be(0.9m);
        rule.ProbabilityMultiplier.Should().Be(1.1m);
    }

    [Fact]
    public void Constructor_NullCapability_ThrowsArgumentNullException()
    {
        var act = () => new ActivityModifierRule(null!, 1.0m, null);
        act.Should().Throw<ArgumentNullException>().WithParameterName("capability");
    }

    [Fact]
    public void Constructor_NeitherMultiplierSet_ThrowsArgumentException()
    {
        var act = () => new ActivityModifierRule(Cap(), null, null);
        act.Should().Throw<ArgumentException>().WithMessage("*least one*");
    }

    [Fact]
    public void Constructor_TimeMultiplierZeroOrNegative_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new ActivityModifierRule(Cap(), 0m, null);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("timeMultiplier");
    }

    [Fact]
    public void Constructor_ProbabilityMultiplierZeroOrNegative_ThrowsArgumentOutOfRangeException()
    {
        var act = () => new ActivityModifierRule(Cap(), null, 0m);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("probabilityMultiplier");
    }
}
