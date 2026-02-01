using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.ValueObjects;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.Entities;

public class ActivityDefinitionTests
{
    private static ActivityModeSupport DefaultModeSupport => new(true, true, null, null);

    private static ActivityAttemptDefinition CreateMinimalAttempt(string key = "attempt_1") =>
        new(key, RollScope.PerPlayer, new AttemptTimeModel(60, TimeDistribution.Uniform), [
            new ActivityOutcomeDefinition("outcome_1", 1, 1, [new ProgressGrant("drop.key", 1)])
        ]);

    [Fact]
    public void Constructor_ValidParameters_CreatesActivityDefinition()
    {
        var key = "activity.zulrah";
        var name = "Zulrah";
        var modeSupport = DefaultModeSupport;

        var entity = new ActivityDefinition(key, name, modeSupport);

        entity.Id.Should().NotBe(Guid.Empty);
        entity.Key.Should().Be(key);
        entity.Name.Should().Be(name);
        entity.ModeSupport.Should().Be(modeSupport);
        entity.Attempts.Should().BeEmpty();
        entity.GroupScalingBands.Should().BeEmpty();
        entity.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyKey_ThrowsArgumentException(string? key)
    {
        var act = () => new ActivityDefinition(key!, "Name", DefaultModeSupport);
        act.Should().Throw<ArgumentException>().WithParameterName("key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyName_ThrowsArgumentException(string? name)
    {
        var act = () => new ActivityDefinition("key", name!, DefaultModeSupport);
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Constructor_NullModeSupport_ThrowsArgumentNullException()
    {
        var act = () => new ActivityDefinition("key", "Name", null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("modeSupport");
    }

    [Fact]
    public void SetAttempts_ValidAttempts_SetsAttempts()
    {
        var entity = new ActivityDefinition("key", "Name", DefaultModeSupport);
        var attempts = new[] { CreateMinimalAttempt("a1"), CreateMinimalAttempt("a2") };

        entity.SetAttempts(attempts);

        entity.Attempts.Should().HaveCount(2);
        entity.Attempts[0].Key.Should().Be("a1");
        entity.Attempts[1].Key.Should().Be("a2");
    }

    [Fact]
    public void SetAttempts_EmptyList_ThrowsArgumentException()
    {
        var entity = new ActivityDefinition("key", "Name", DefaultModeSupport);
        var act = () => entity.SetAttempts([]);
        act.Should().Throw<ArgumentException>().WithParameterName("attempts");
    }

    [Fact]
    public void SetAttempts_DuplicateKeys_ThrowsInvalidOperationException()
    {
        var entity = new ActivityDefinition("key", "Name", DefaultModeSupport);
        var attempts = new[] { CreateMinimalAttempt("same"), CreateMinimalAttempt("same") };
        var act = () => entity.SetAttempts(attempts);
        act.Should().Throw<InvalidOperationException>().WithMessage("*unique*");
    }

    [Fact]
    public void SetGroupScalingBands_ValidBands_SetsBands()
    {
        var entity = new ActivityDefinition("key", "Name", DefaultModeSupport);
        var bands = new[] { new GroupSizeBand(1, 1, 1.0m, 1.0m), new GroupSizeBand(2, 4, 0.9m, 1.1m) };

        entity.SetGroupScalingBands(bands);

        entity.GroupScalingBands.Should().HaveCount(2);
        entity.GroupScalingBands[0].MinSize.Should().Be(1);
        entity.GroupScalingBands[1].MaxSize.Should().Be(4);
    }

    [Fact]
    public void UpdateKey_ValidKey_UpdatesKey()
    {
        var entity = new ActivityDefinition("old", "Name", DefaultModeSupport);
        entity.UpdateKey("new_key");
        entity.Key.Should().Be("new_key");
    }

    [Fact]
    public void UpdateName_ValidName_UpdatesName()
    {
        var entity = new ActivityDefinition("key", "OldName", DefaultModeSupport);
        entity.UpdateName("NewName");
        entity.Name.Should().Be("NewName");
    }
}
