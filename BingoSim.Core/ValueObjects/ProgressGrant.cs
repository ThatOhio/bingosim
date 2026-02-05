namespace BingoSim.Core.ValueObjects;

/// <summary>
/// Represents a grant of progress units for a particular DropKey.
/// Supports fixed units or a variable range (UnitsMin–UnitsMax) sampled at runtime.
/// </summary>
public sealed record ProgressGrant
{
    public string DropKey { get; init; } = string.Empty;
    /// <summary>Fixed units when UnitsMin/UnitsMax are not set.</summary>
    public int Units { get; init; }
    /// <summary>Minimum units for variable grants. When set with UnitsMax, units are sampled uniformly at runtime.</summary>
    public int? UnitsMin { get; init; }
    /// <summary>Maximum units for variable grants. When set with UnitsMin, units are sampled uniformly at runtime.</summary>
    public int? UnitsMax { get; init; }

    /// <summary>
    /// Creates a fixed grant (constant units per outcome).
    /// </summary>
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
    /// Creates a variable grant (units sampled uniformly from min to max at runtime).
    /// </summary>
    public ProgressGrant(string dropKey, int unitsMin, int unitsMax)
    {
        if (string.IsNullOrWhiteSpace(dropKey))
            throw new ArgumentException("Drop key cannot be empty.", nameof(dropKey));

        if (unitsMin < 1)
            throw new ArgumentOutOfRangeException(nameof(unitsMin), "UnitsMin must be at least 1.");

        if (unitsMax < 1)
            throw new ArgumentOutOfRangeException(nameof(unitsMax), "UnitsMax must be at least 1.");

        if (unitsMin > unitsMax)
            throw new ArgumentOutOfRangeException(nameof(unitsMax), "UnitsMax must be greater than or equal to UnitsMin.");

        DropKey = dropKey.Trim();
        Units = unitsMin; // Store min for display/fallback; EF may need a non-null value
        UnitsMin = unitsMin;
        UnitsMax = unitsMax;
    }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization from JSON.
    /// </summary>
    private ProgressGrant()
    {
    }

    /// <summary>
    /// True when this grant uses variable units (UnitsMin and UnitsMax both set).
    /// </summary>
    public bool IsVariable => UnitsMin.HasValue && UnitsMax.HasValue;
}
