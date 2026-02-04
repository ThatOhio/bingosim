using BingoSim.Core.Entities;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.Entities;

public class StrategyConfigTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesStrategyConfig()
    {
        var teamId = Guid.NewGuid();
        var config = new StrategyConfig(teamId, "RowUnlocking", "{\"key\":\"value\"}");

        config.Id.Should().NotBe(Guid.Empty);
        config.TeamId.Should().Be(teamId);
        config.StrategyKey.Should().Be("RowUnlocking");
        config.ParamsJson.Should().Be("{\"key\":\"value\"}");
    }

    [Fact]
    public void Constructor_NullParamsJson_StoresNull()
    {
        var config = new StrategyConfig(Guid.NewGuid(), "RowUnlocking", null);
        config.ParamsJson.Should().BeNull();
    }

    [Fact]
    public void Constructor_DefaultTeamId_ThrowsArgumentException()
    {
        var act = () => new StrategyConfig(Guid.Empty, "RowUnlocking", null);
        act.Should().Throw<ArgumentException>().WithParameterName("teamId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyStrategyKey_ThrowsArgumentException(string? key)
    {
        var act = () => new StrategyConfig(Guid.NewGuid(), key!, null);
        act.Should().Throw<ArgumentException>().WithParameterName("strategyKey");
    }

    [Fact]
    public void Update_ValidParameters_Updates()
    {
        var config = new StrategyConfig(Guid.NewGuid(), "RowUnlocking", null);
        config.Update("RowUnlocking", "{\"x\":1}");
        config.StrategyKey.Should().Be("RowUnlocking");
        config.ParamsJson.Should().Be("{\"x\":1}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_EmptyStrategyKey_ThrowsArgumentException(string? key)
    {
        var config = new StrategyConfig(Guid.NewGuid(), "RowUnlocking", null);
        var act = () => config.Update(key!, null);
        act.Should().Throw<ArgumentException>().WithParameterName("strategyKey");
    }
}
