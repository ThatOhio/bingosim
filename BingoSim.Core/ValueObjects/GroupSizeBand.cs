namespace BingoSim.Core.ValueObjects;

/// <summary>
/// Defines scaling for a range of group sizes (time and probability multipliers).
/// </summary>
public sealed record GroupSizeBand
{
    public int MinSize { get; init; }
    public int MaxSize { get; init; }
    public decimal TimeMultiplier { get; init; }
    public decimal ProbabilityMultiplier { get; init; }

    public GroupSizeBand(int minSize, int maxSize, decimal timeMultiplier, decimal probabilityMultiplier)
    {
        if (minSize < 1)
            throw new ArgumentOutOfRangeException(nameof(minSize), "Min size must be at least 1.");

        if (maxSize < minSize)
            throw new ArgumentException("Max size cannot be less than min size.", nameof(maxSize));

        if (timeMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeMultiplier), "Time multiplier must be greater than zero.");

        if (probabilityMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(probabilityMultiplier), "Probability multiplier must be greater than zero.");

        MinSize = minSize;
        MaxSize = maxSize;
        TimeMultiplier = timeMultiplier;
        ProbabilityMultiplier = probabilityMultiplier;
    }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization from JSON.
    /// </summary>
    private GroupSizeBand()
    {
    }
}
