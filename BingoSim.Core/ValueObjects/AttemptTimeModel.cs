using BingoSim.Core.Enums;

namespace BingoSim.Core.ValueObjects;

/// <summary>
/// Defines how attempt time is sampled.
/// </summary>
public sealed record AttemptTimeModel
{
    public int BaselineTimeSeconds { get; init; }
    public TimeDistribution Distribution { get; init; }
    public int? VarianceSeconds { get; init; }

    public AttemptTimeModel(int baselineTimeSeconds, TimeDistribution distribution, int? varianceSeconds = null)
    {
        if (baselineTimeSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(baselineTimeSeconds), "Baseline time must be greater than zero.");

        if (varianceSeconds.HasValue && varianceSeconds.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(varianceSeconds), "Variance cannot be negative.");

        BaselineTimeSeconds = baselineTimeSeconds;
        Distribution = distribution;
        VarianceSeconds = varianceSeconds;
    }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization from JSON.
    /// </summary>
    private AttemptTimeModel()
    {
    }
}
