namespace BingoSim.Core.ValueObjects;

/// <summary>
/// Represents a grant of progress units for a particular DropKey.
/// </summary>
public sealed record ProgressGrant
{
    public string DropKey { get; init; } = string.Empty;
    public int Units { get; init; }

    public ProgressGrant(string dropKey, int units)
    {
        if (string.IsNullOrWhiteSpace(dropKey))
            throw new ArgumentException("Drop key cannot be empty.", nameof(dropKey));

        if (units < 1)
            throw new ArgumentOutOfRangeException(nameof(units), "Units must be at least 1.");

        DropKey = dropKey.Trim();
        Units = units;
    }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization from JSON.
    /// </summary>
    private ProgressGrant()
    {
    }
}
