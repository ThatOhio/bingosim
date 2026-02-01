namespace BingoSim.Core.ValueObjects;

/// <summary>
/// Represents one possible outcome of a roll with a probability weight.
/// </summary>
public sealed record ActivityOutcomeDefinition
{
    public string Key { get; init; } = string.Empty;
    public int WeightNumerator { get; init; }
    public int WeightDenominator { get; init; }
    /// <summary>Grants (private set for EF Core JSON deserialization).</summary>
    public List<ProgressGrant> Grants { get; private set; } = [];

    public ActivityOutcomeDefinition(string key, int weightNumerator, int weightDenominator, IEnumerable<ProgressGrant>? grants = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Outcome key cannot be empty.", nameof(key));

        if (weightNumerator <= 0)
            throw new ArgumentOutOfRangeException(nameof(weightNumerator), "Weight numerator must be greater than zero.");

        if (weightDenominator <= 0)
            throw new ArgumentOutOfRangeException(nameof(weightDenominator), "Weight denominator must be greater than zero.");

        Key = key.Trim();
        WeightNumerator = weightNumerator;
        WeightDenominator = weightDenominator;
        Grants = grants?.ToList() ?? [];
    }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization from JSON.
    /// </summary>
    private ActivityOutcomeDefinition()
    {
        Key = string.Empty;
    }
}
