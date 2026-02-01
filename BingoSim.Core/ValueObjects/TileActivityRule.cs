namespace BingoSim.Core.ValueObjects;

/// <summary>
/// Defines how a particular Activity contributes progress to a Tile.
/// ActivityDefinitionId is source of truth for logic/joins; ActivityKey is denormalized for display/debug.
/// </summary>
public sealed record TileActivityRule
{
    public Guid ActivityDefinitionId { get; init; }
    public string ActivityKey { get; init; } = string.Empty;
    /// <summary>Private set for EF Core JSON deserialization.</summary>
    public List<string> AcceptedDropKeys { get; private set; } = [];
    /// <summary>Private set for EF Core JSON deserialization.</summary>
    public List<Capability> Requirements { get; private set; } = [];
    /// <summary>Private set for EF Core JSON deserialization.</summary>
    public List<ActivityModifierRule> Modifiers { get; private set; } = [];

    public TileActivityRule(
        Guid activityDefinitionId,
        string activityKey,
        IEnumerable<string> acceptedDropKeys,
        IEnumerable<Capability> requirements,
        IEnumerable<ActivityModifierRule> modifiers)
    {
        if (activityDefinitionId == Guid.Empty)
            throw new ArgumentException("ActivityDefinitionId cannot be empty.", nameof(activityDefinitionId));

        ArgumentNullException.ThrowIfNull(activityKey);
        ArgumentNullException.ThrowIfNull(acceptedDropKeys);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(modifiers);

        ActivityDefinitionId = activityDefinitionId;
        ActivityKey = activityKey;
        AcceptedDropKeys = acceptedDropKeys.ToList();
        Requirements = requirements.ToList();
        Modifiers = modifiers.ToList();
    }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization from JSON.
    /// </summary>
    private TileActivityRule()
    {
    }
}
