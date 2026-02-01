using BingoSim.Core.Enums;

namespace BingoSim.Core.ValueObjects;

/// <summary>
/// Represents one roll channel / loot line for an activity.
/// </summary>
public sealed record ActivityAttemptDefinition
{
    public string Key { get; init; } = string.Empty;
    public RollScope RollScope { get; init; }
    public AttemptTimeModel TimeModel { get; init; } = null!;
    /// <summary>Outcomes (private set for EF Core JSON deserialization).</summary>
    public List<ActivityOutcomeDefinition> Outcomes { get; private set; } = [];

    public ActivityAttemptDefinition(string key, RollScope rollScope, AttemptTimeModel timeModel, IEnumerable<ActivityOutcomeDefinition> outcomes)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Attempt key cannot be empty.", nameof(key));

        ArgumentNullException.ThrowIfNull(timeModel);
        ArgumentNullException.ThrowIfNull(outcomes);

        var outcomeList = outcomes.ToList();
        if (outcomeList.Count == 0)
            throw new ArgumentException("At least one outcome is required.", nameof(outcomes));

        Key = key.Trim();
        RollScope = rollScope;
        TimeModel = timeModel;
        Outcomes = outcomeList;
    }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization from JSON.
    /// </summary>
    private ActivityAttemptDefinition()
    {
        Key = string.Empty;
        TimeModel = null!;
    }
}
