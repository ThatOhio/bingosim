namespace BingoSim.Core.ValueObjects;

/// <summary>
/// Defines an optional capability-based modifier (time/probability multipliers).
/// </summary>
public sealed record ActivityModifierRule
{
    public Capability Capability { get; init; } = null!;
    public decimal? TimeMultiplier { get; init; }
    public decimal? ProbabilityMultiplier { get; init; }

    public ActivityModifierRule(Capability capability, decimal? timeMultiplier, decimal? probabilityMultiplier)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (!timeMultiplier.HasValue && !probabilityMultiplier.HasValue)
            throw new ArgumentException("At least one of TimeMultiplier or ProbabilityMultiplier must be set.");

        if (timeMultiplier.HasValue && timeMultiplier.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeMultiplier), "Time multiplier must be greater than zero.");

        if (probabilityMultiplier.HasValue && probabilityMultiplier.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(probabilityMultiplier), "Probability multiplier must be greater than zero.");

        Capability = capability;
        TimeMultiplier = timeMultiplier;
        ProbabilityMultiplier = probabilityMultiplier;
    }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization from JSON.
    /// </summary>
    private ActivityModifierRule()
    {
    }
}
