using BingoSim.Core.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BingoSim.Core.UnitTests.ValueObjects;

public class TileActivityRuleTests
{
    private static Guid ActivityId => Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static TileActivityRule CreateValid() => new(
        ActivityId,
        "activity.zulrah",
        ["drop.magic_fang"],
        [new Capability("quest.ds2", "Dragon Slayer 2")],
        []);

    [Fact]
    public void Constructor_ValidParameters_CreatesRule()
    {
        var rule = CreateValid();

        rule.ActivityDefinitionId.Should().Be(ActivityId);
        rule.ActivityKey.Should().Be("activity.zulrah");
        rule.AcceptedDropKeys.Should().ContainSingle("drop.magic_fang");
        rule.Requirements.Should().ContainSingle().Which.Key.Should().Be("quest.ds2");
        rule.Modifiers.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_EmptyActivityDefinitionId_ThrowsArgumentException()
    {
        var act = () => new TileActivityRule(
            Guid.Empty,
            "activity.key",
            [],
            [],
            []);

        act.Should().Throw<ArgumentException>().WithParameterName("activityDefinitionId");
    }

    [Fact]
    public void Constructor_WithModifiers_PersistsModifiers()
    {
        var mod = new ActivityModifierRule(new Capability("item.dhl", "DHL"), 0.9m, null);
        var rule = new TileActivityRule(ActivityId, "activity.key", [], [], [mod]);

        rule.Modifiers.Should().ContainSingle().Which.Capability.Key.Should().Be("item.dhl");
    }
}
