namespace BingoSim.Core.Entities;

/// <summary>
/// Represents a drafted team for a specific event instance. Teams are per-event only.
/// </summary>
public class Team
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Navigation for EF Core; 1:1 with StrategyConfig.</summary>
    public StrategyConfig? StrategyConfig { get; private set; }

    private readonly List<TeamPlayer> _teamPlayers = [];
    /// <summary>Navigation for EF Core; many TeamPlayers.</summary>
    public IReadOnlyList<TeamPlayer> TeamPlayers => _teamPlayers;

    /// <summary>Parameterless constructor for EF Core.</summary>
    private Team() { }

    public Team(Guid eventId, string name)
    {
        if (eventId == default)
            throw new ArgumentException("EventId cannot be empty.", nameof(eventId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Id = Guid.NewGuid();
        EventId = eventId;
        Name = name.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Name = name.Trim();
    }
}
