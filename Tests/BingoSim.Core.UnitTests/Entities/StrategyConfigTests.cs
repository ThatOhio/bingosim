using BingoSim.Core.Entities;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.Entities;

public class StrategyConfigTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesStrategyConfig()
    {
        var teamId = Guid.NewGuid();
        var config = new StrategyConfig(teamId, "RowRush", "{\"key\":\"value\"}");

        config.Id.Should().NotBe(Guid.Empty);
        config.TeamId.Should().Be(teamId);
        config.StrategyKey.Should().Be("RowRush");
        config.ParamsJson.Should().Be("{\"key\":\"value\"}");
    }

    [Fact]
    public void Constructor_NullParamsJson_StoresNull()
    {
        var config = new StrategyConfig(Guid.NewGuid(), "GreedyPoints", null);
        config.ParamsJson.Should().BeNull();
    }

    [Fact]
    public void Constructor_DefaultTeamId_ThrowsArgumentException()
    {
        var act = () => new StrategyConfig(Guid.Empty, "RowRush", null);
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
        var config = new StrategyConfig(Guid.NewGuid(), "RowRush", null);
        config.Update("GreedyPoints", "{\"x\":1}");
        config.StrategyKey.Should().Be("GreedyPoints");
        config.ParamsJson.Should().Be("{\"x\":1}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_EmptyStrategyKey_ThrowsArgumentException(string? key)
    {
        var config = new StrategyConfig(Guid.NewGuid(), "RowRush", null);
        var act = () => config.Update(key!, null);
        act.Should().Throw<ArgumentException>().WithParameterName("strategyKey");
    }
}
