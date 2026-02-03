using BingoSim.Application.Simulation.Snapshot;

namespace BingoSim.Application.Simulation;

/// <summary>
/// Pure functions for group scaling band selection and multiplier stacking.
/// Band selection: first band where MinSize &lt;= groupSize &lt;= MaxSize.
/// Effective multipliers = group * modifier (multiplicative stacking).
/// </summary>
public static class GroupScalingBandSelector
{
    /// <summary>
    /// Returns (TimeMultiplier, ProbabilityMultiplier) for the given group size.
    /// Uses the first band where MinSize &lt;= groupSize &lt;= MaxSize.
    /// If no band matches or bands is null/empty, returns (1.0, 1.0).
    /// </summary>
    public static (decimal TimeMultiplier, decimal ProbabilityMultiplier) Select(
        IReadOnlyList<GroupSizeBandSnapshotDto>? bands,
        int groupSize)
    {
        if (bands is not { Count: > 0 })
            return (1.0m, 1.0m);

        foreach (var band in bands)
        {
            if (groupSize >= band.MinSize && groupSize <= band.MaxSize)
                return (band.TimeMultiplier, band.ProbabilityMultiplier);
        }

        return (1.0m, 1.0m);
    }

    /// <summary>
    /// Computes effective time and probability multipliers by stacking group scaling with modifiers.
    /// effectiveTime = groupTime * modifierTime; effectiveProb = groupProb * modifierProb.
    /// </summary>
    public static (decimal EffectiveTime, decimal EffectiveProb) ComputeEffectiveMultipliers(
        IReadOnlyList<GroupSizeBandSnapshotDto>? bands,
        int groupSize,
        TileActivityRuleSnapshotDto? rule,
        IReadOnlySet<string> capabilityKeys)
    {
        var (groupTime, groupProb) = Select(bands, groupSize);
        var (modTime, modProb) = ModifierApplicator.ComputeCombinedMultipliers(rule, capabilityKeys);
        return (groupTime * modTime, groupProb * modProb);
    }
}
