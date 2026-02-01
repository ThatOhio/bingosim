using BingoSim.Core.Enums;
using BingoSim.Core.ValueObjects;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.ValueObjects;

public class ActivityAttemptDefinitionTests
{
    private static ActivityOutcomeDefinition MinimalOutcome =>
        new("outcome_1", 1, 1, [new ProgressGrant("drop.key", 1)]);

    private static AttemptTimeModel DefaultTimeModel =>
        new AttemptTimeModel(60, TimeDistribution.Uniform);

    [Fact]
    public void Constructor_ValidParameters_CreatesValueObject()
    {
        var vo = new ActivityAttemptDefinition("personal_loot_1", RollScope.PerPlayer, DefaultTimeModel, [MinimalOutcome]);
        vo.Key.Should().Be("personal_loot_1");
        vo.RollScope.Should().Be(RollScope.PerPlayer);
        vo.TimeModel.BaselineTimeSeconds.Should().Be(60);
        vo.Outcomes.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyKey_Throws(string? key)
    {
        var act = () => new ActivityAttemptDefinition(key!, RollScope.PerPlayer, DefaultTimeModel, [MinimalOutcome]);
        act.Should().Throw<ArgumentException>().WithParameterName("key");
    }

    [Fact]
    public void Constructor_NoOutcomes_Throws()
    {
        var act = () => new ActivityAttemptDefinition("key", RollScope.PerPlayer, DefaultTimeModel, []);
        act.Should().Throw<ArgumentException>().WithParameterName("outcomes");
    }
}
