using BingoSim.Core.Entities;
using FluentAssertions;

namespace BingoSim.Core.UnitTests.Entities;

public class TeamTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesTeam()
    {
        var eventId = Guid.NewGuid();
        var team = new Team(eventId, "Team Alpha");

        team.Id.Should().NotBe(Guid.Empty);
        team.EventId.Should().Be(eventId);
        team.Name.Should().Be("Team Alpha");
        team.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_DefaultEventId_ThrowsArgumentException()
    {
        var act = () => new Team(Guid.Empty, "Team Alpha");
        act.Should().Throw<ArgumentException>().WithParameterName("eventId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyName_ThrowsArgumentException(string? name)
    {
        var act = () => new Team(Guid.NewGuid(), name!);
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void UpdateName_ValidName_Updates()
    {
        var team = new Team(Guid.NewGuid(), "Original");
        team.UpdateName("Updated");
        team.Name.Should().Be("Updated");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateName_EmptyName_ThrowsArgumentException(string? name)
    {
        var team = new Team(Guid.NewGuid(), "Original");
        var act = () => team.UpdateName(name!);
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }
}
