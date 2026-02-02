namespace BingoSim.Core.Entities;

/// <summary>
/// Membership entity linking a reusable PlayerProfile to a specific Team.
/// </summary>
public class TeamPlayer
{
    public Guid Id { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid PlayerProfileId { get; private set; }

    /// <summary>Navigation for EF Core.</summary>
    public Team? Team { get; private set; }

    /// <summary>Navigation for EF Core.</summary>
    public PlayerProfile? PlayerProfile { get; private set; }

    /// <summary>Parameterless constructor for EF Core.</summary>
    private TeamPlayer() { }

    public TeamPlayer(Guid teamId, Guid playerProfileId)
    {
        if (teamId == default)
            throw new ArgumentException("TeamId cannot be empty.", nameof(teamId));

        if (playerProfileId == default)
            throw new ArgumentException("PlayerProfileId cannot be empty.", nameof(playerProfileId));

        Id = Guid.NewGuid();
        TeamId = teamId;
        PlayerProfileId = playerProfileId;
    }
}
