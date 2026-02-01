namespace BingoSim.Core.ValueObjects;

/// <summary>
/// Represents an event-specific goal worth points (1-4). Each row contains exactly 4 tiles with points 1,2,3,4.
/// </summary>
public sealed record Tile
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Points { get; init; }
    public int RequiredCount { get; init; }
    /// <summary>Private set for EF Core JSON deserialization.</summary>
    public List<TileActivityRule> AllowedActivities { get; private set; } = [];

    public Tile(string key, string name, int points, int requiredCount, IEnumerable<TileActivityRule> allowedActivities)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Tile key cannot be empty.", nameof(key));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tile name cannot be empty.", nameof(name));

        if (points is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(points), "Points must be 1, 2, 3, or 4.");

        if (requiredCount < 1)
            throw new ArgumentOutOfRangeException(nameof(requiredCount), "RequiredCount must be at least 1.");

        ArgumentNullException.ThrowIfNull(allowedActivities);

        var rules = allowedActivities.ToList();
        if (rules.Count == 0)
            throw new ArgumentException("At least one TileActivityRule is required.", nameof(allowedActivities));

        Key = key.Trim();
        Name = name.Trim();
        Points = points;
        RequiredCount = requiredCount;
        AllowedActivities = rules;
    }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization from JSON.
    /// </summary>
    private Tile()
    {
    }
}
