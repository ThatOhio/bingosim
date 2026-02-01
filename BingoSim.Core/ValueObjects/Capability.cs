namespace BingoSim.Core.ValueObjects;

/// <summary>
/// Represents a player attribute that may gate eligibility for activities
/// or provide optional modifiers.
/// </summary>
public sealed record Capability
{
    /// <summary>
    /// Stable identifier (e.g., "quest.ds2", "item.dragon_hunter_lance").
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Display name for the capability.
    /// </summary>
    public string Name { get; }

    public Capability(string key, string name)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Key = key;
        Name = name;
    }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization from JSON.
    /// </summary>
    private Capability()
    {
        Key = string.Empty;
        Name = string.Empty;
    }
}
