using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Simulation;

/// <summary>
/// Pure functions for modifier multiplier computation and probability scaling.
/// Centralizes logic; used by SimulationRunner.
/// </summary>
public static class ModifierApplicator
{
    private const int MaxAdjustedWeight = 1_000_000_000;

    /// <summary>
    /// Computes combined time and probability multipliers in a single pass.
    /// Returns (1.0, 1.0) if Modifiers is empty or no applicable modifiers.
    /// </summary>
    public static (decimal Time, decimal Probability) ComputeCombinedMultipliers(
        TileActivityRuleSnapshotDto rule,
        IReadOnlySet<string> playerCapabilityKeys)
    {
        if (rule.Modifiers.Count == 0)
            return (1.0m, 1.0m);

        decimal timeProduct = 1.0m;
        decimal probProduct = 1.0m;
        foreach (var mod in rule.Modifiers)
        {
            if (!playerCapabilityKeys.Contains(mod.CapabilityKey))
                continue;
            if (mod.TimeMultiplier.HasValue)
                timeProduct *= mod.TimeMultiplier.Value;
            if (mod.ProbabilityMultiplier.HasValue)
                probProduct *= mod.ProbabilityMultiplier.Value;
        }
        return (timeProduct, probProduct);
    }

    /// <summary>
    /// Computes combined time multiplier. Delegates to ComputeCombinedMultipliers.
    /// </summary>
    public static decimal ComputeCombinedTimeMultiplier(
        TileActivityRuleSnapshotDto rule,
        IReadOnlySet<string> playerCapabilityKeys) =>
        ComputeCombinedMultipliers(rule, playerCapabilityKeys).Time;

    /// <summary>
    /// Computes combined probability multiplier. Delegates to ComputeCombinedMultipliers.
    /// </summary>
    public static decimal ComputeCombinedProbabilityMultiplier(
        TileActivityRuleSnapshotDto rule,
        IReadOnlySet<string> playerCapabilityKeys) =>
        ComputeCombinedMultipliers(rule, playerCapabilityKeys).Probability;

    /// <summary>
    /// Returns adjusted weights for outcomes. Outcomes whose grants include a DropKey in acceptedDropKeys
    /// get their weight multiplied by probabilityMultiplier. Clamps to [0, MaxAdjustedWeight].
    /// If total weight would be 0, returns original weights.
    /// </summary>
    /// <param name="acceptedDropKeys">Drop keys relevant to the tile. Empty => no scaling.</param>
    public static IReadOnlyList<int> ApplyProbabilityMultiplier(
        IReadOnlyList<OutcomeSnapshotDto> outcomes,
        IReadOnlyCollection<string> acceptedDropKeys,
        decimal probabilityMultiplier)
    {
        if (probabilityMultiplier == 1.0m || acceptedDropKeys.Count == 0)
            return outcomes.Select(o => o.WeightNumerator).ToList();

        var adjusted = new int[outcomes.Count];
        var total = 0L;
        for (var i = 0; i < outcomes.Count; i++)
        {
            var o = outcomes[i];
            var isRelevant = o.Grants.Any(g => acceptedDropKeys.Contains(g.DropKey));
            var raw = isRelevant
                ? (long)Math.Round(o.WeightNumerator * probabilityMultiplier)
                : o.WeightNumerator;
            var clamped = Math.Clamp(raw, 0, MaxAdjustedWeight);
            adjusted[i] = (int)clamped;
            total += clamped;
        }

        if (total <= 0)
            return outcomes.Select(o => o.WeightNumerator).ToList();

        return adjusted;
    }
}
