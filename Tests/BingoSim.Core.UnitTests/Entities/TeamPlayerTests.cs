using BingoSim.Core.Entities;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.Entities;

public class TeamPlayerTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesTeamPlayer()
    {
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var tp = new TeamPlayer(teamId, playerId);

        tp.Id.Should().NotBe(Guid.Empty);
        tp.TeamId.Should().Be(teamId);
        tp.PlayerProfileId.Should().Be(playerId);
    }

    [Fact]
    public void Constructor_DefaultTeamId_ThrowsArgumentException()
    {
        var act = () => new TeamPlayer(Guid.Empty, Guid.NewGuid());
        act.Should().Throw<ArgumentException>().WithParameterName("teamId");
    }

    [Fact]
    public void Constructor_DefaultPlayerProfileId_ThrowsArgumentException()
    {
        var act = () => new TeamPlayer(Guid.NewGuid(), Guid.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("playerProfileId");
    }
}
