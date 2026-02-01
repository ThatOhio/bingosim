namespace BingoSim.Core.ValueObjects;

/// <summary>
/// Declares which group sizes are supported for an activity.
/// </summary>
public sealed record ActivityModeSupport
{
    public bool SupportsSolo { get; init; }
    public bool SupportsGroup { get; init; }
    public int? MinGroupSize { get; init; }
    public int? MaxGroupSize { get; init; }

    public ActivityModeSupport(bool supportsSolo, bool supportsGroup, int? minGroupSize, int? maxGroupSize)
    {
        SupportsSolo = supportsSolo;
        SupportsGroup = supportsGroup;

        if (minGroupSize.HasValue && minGroupSize.Value < 1)
            throw new ArgumentOutOfRangeException(nameof(minGroupSize), "Min group size must be at least 1.");

        if (maxGroupSize.HasValue && maxGroupSize.Value < 1)
            throw new ArgumentOutOfRangeException(nameof(maxGroupSize), "Max group size must be at least 1.");

        if (minGroupSize.HasValue && maxGroupSize.HasValue && minGroupSize.Value > maxGroupSize.Value)
            throw new ArgumentException("Min group size cannot exceed max group size.", nameof(minGroupSize));

        MinGroupSize = minGroupSize;
        MaxGroupSize = maxGroupSize;
    }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization from JSON.
    /// </summary>
    private ActivityModeSupport()
    {
        MinGroupSize = null;
        MaxGroupSize = null;
    }
}
