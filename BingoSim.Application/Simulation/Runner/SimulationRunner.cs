using System.Collections.Generic;
using System.Text.Json;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Snapshot;
namespace BingoSim.Application.Simulation.Runner;

/// <summary>
/// Executes one simulation run from a snapshot and produces per-team results.
/// Uses deterministic RNG from run seed string for reproducibility.
/// </summary>
public class SimulationRunner(IProgressAllocatorFactory allocatorFactory)
{
    private const int MaxAttemptsPerPlayerPerSecond = 10;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null, WriteIndented = false };

    /// <summary>
    /// Runs simulation and returns per-team results (aggregates + timeline JSON).
    /// </summary>
    public IReadOnlyList<TeamRunResultDto> Execute(string snapshotJson, string runSeedString, CancellationToken cancellationToken = default)
    {
        var snapshot = EventSnapshotBuilder.Deserialize(snapshotJson);
        if (snapshot is null)
            throw new InvalidOperationException("Invalid snapshot JSON.");

        var rngSeed = SeedDerivation.DeriveRngSeed(runSeedString, 0);
        var rng = new Random(rngSeed);

        var durationSeconds = snapshot.DurationSeconds;
        var unlockRequired = snapshot.UnlockPointsRequiredPerRow;
        var rows = snapshot.Rows.OrderBy(r => r.Index).ToList();
        var totalRowCount = rows.Count;

        var tileRowIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var tilePoints = new Dictionary<string, int>(StringComparer.Ordinal);
        var tileRequiredCount = new Dictionary<string, int>(StringComparer.Ordinal);
        var tileToRules = new Dictionary<string, List<TileActivityRuleSnapshotDto>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            foreach (var tile in row.Tiles)
            {
                tileRowIndex[tile.Key] = row.Index;
                tilePoints[tile.Key] = tile.Points;
                tileRequiredCount[tile.Key] = tile.RequiredCount;
                tileToRules[tile.Key] = tile.AllowedActivities;
            }
        }

        var teamStates = new List<TeamRunState>();
        foreach (var team in snapshot.Teams)
        {
            var state = new TeamRunState(
                team.TeamId,
                team.TeamName,
                team.StrategyKey,
                team.ParamsJson,
                rows.Count,
                tileRowIndex,
                tilePoints,
                tileRequiredCount,
                unlockRequired);
            teamStates.Add(state);
        }

        var eventQueue = new PriorityQueue<SimEvent, int>();
        var simTime = 0;
        for (var ti = 0; ti < snapshot.Teams.Count; ti++)
        {
            var team = snapshot.Teams[ti];
            var state = teamStates[ti];
            for (var pi = 0; pi < team.Players.Count; pi++)
            {
                var (activityId, attemptKey) = GetFirstEligibleActivity(snapshot, team, pi, state.UnlockedRowIndices, state.CompletedTiles);
                if (activityId is null)
                    continue;
                var duration = SampleAttemptDuration(snapshot, activityId.Value, attemptKey!, team.Players[pi].SkillTimeMultiplier, rng);
                var endTime = Math.Min(simTime + duration, durationSeconds + 1);
                eventQueue.Enqueue(new SimEvent(endTime, ti, pi, activityId.Value, attemptKey!), endTime);
            }
        }

        while (eventQueue.Count > 0 && simTime <= durationSeconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!eventQueue.TryDequeue(out var evt, out _))
                break;
            simTime = evt.SimTime;
            if (simTime > durationSeconds)
                break;

            var ti = evt.TeamIndex;
            var pi = evt.PlayerIndex;
            var team = snapshot.Teams[ti];
            var state = teamStates[ti];
            var player = team.Players[pi];

            var activityId = evt.ActivityId;
            var attemptKey = evt.AttemptKey;
            var activity = snapshot.ActivitiesById.GetValueOrDefault(activityId);
            if (activity is null)
                continue;
            var attempt = activity.Attempts.FirstOrDefault(a => a.Key == attemptKey);
            if (attempt is null)
                attempt = activity.Attempts[0];

            var outcome = RollOutcome(attempt, rng);
            if (outcome is null)
                continue;

            var allocator = allocatorFactory.GetAllocator(team.StrategyKey);
            foreach (var grant in outcome.Grants)
            {
                var eligible = GetEligibleTileKeys(snapshot, state, grant.DropKey, tileRowIndex, tilePoints, tileRequiredCount, tileToRules);
                if (eligible.Count == 0)
                    continue;
                var context = new AllocatorContext
                {
                    UnlockedRowIndices = state.UnlockedRowIndices,
                    TileProgress = state.TileProgress,
                    TileRequiredCount = tileRequiredCount,
                    TileRowIndex = tileRowIndex,
                    TilePoints = tilePoints,
                    EligibleTileKeys = eligible
                };
                var target = allocator.SelectTargetTile(context);
                if (target is null)
                    continue;
                state.AddProgress(target, grant.Units, simTime, tileRequiredCount[target], tileRowIndex[target], tilePoints[target]);
            }

            var (nextActivityId, nextAttemptKey) = GetFirstEligibleActivity(snapshot, team, pi, state.UnlockedRowIndices, state.CompletedTiles);
            if (nextActivityId is not null && nextAttemptKey is not null)
            {
                var nextDuration = SampleAttemptDuration(snapshot, nextActivityId.Value, nextAttemptKey, player.SkillTimeMultiplier, rng);
                var nextEnd = Math.Min(simTime + nextDuration, durationSeconds + 1);
                eventQueue.Enqueue(new SimEvent(nextEnd, ti, pi, nextActivityId.Value, nextAttemptKey), nextEnd);
            }
        }

        var maxPoints = teamStates.Max(s => s.TotalPoints);
        var winners = teamStates.Where(s => s.TotalPoints == maxPoints).ToList();
        var winnerId = winners.Count > 0 ? winners[0].TeamId : Guid.Empty;

        var results = new List<TeamRunResultDto>();
        foreach (var state in teamStates)
        {
            var rowUnlockJson = JsonSerializer.Serialize(state.RowUnlockTimes, JsonOptions);
            var tileCompletionJson = JsonSerializer.Serialize(state.TileCompletionTimes, JsonOptions);
            results.Add(new TeamRunResultDto
            {
                TeamId = state.TeamId,
                TeamName = state.TeamName,
                StrategyKey = state.StrategyKey,
                ParamsJson = state.ParamsJson,
                TotalPoints = state.TotalPoints,
                TilesCompletedCount = state.TilesCompletedCount,
                RowReached = state.RowReached,
                IsWinner = state.TeamId == winnerId,
                RowUnlockTimesJson = rowUnlockJson,
                TileCompletionTimesJson = tileCompletionJson
            });
        }

        return results;
    }

    private static (Guid? activityId, string? attemptKey) GetFirstEligibleActivity(
        EventSnapshotDto snapshot,
        TeamSnapshotDto team,
        int playerIndex,
        IReadOnlySet<int> unlockedRows,
        IReadOnlySet<string> completedTiles)
    {
        var player = team.Players[playerIndex];
        var playerCaps = new HashSet<string>(player.CapabilityKeys, StringComparer.Ordinal);
        foreach (var row in snapshot.Rows.OrderBy(r => r.Index))
        {
            if (!unlockedRows.Contains(row.Index))
                continue;
            foreach (var tile in row.Tiles.OrderBy(t => t.Points))
            {
                if (completedTiles.Contains(tile.Key))
                    continue;
                foreach (var rule in tile.AllowedActivities)
                {
                    if (rule.RequirementKeys.Count > 0 && !rule.RequirementKeys.All(playerCaps.Contains))
                        continue;
                    var activity = snapshot.ActivitiesById.GetValueOrDefault(rule.ActivityDefinitionId);
                    if (activity is null || activity.Attempts.Count == 0)
                        continue;
                    return (rule.ActivityDefinitionId, activity.Attempts[0].Key);
                }
            }
        }
        return (null, null);
    }

    private static List<string> GetEligibleTileKeys(
        EventSnapshotDto snapshot,
        TeamRunState state,
        string dropKey,
        IReadOnlyDictionary<string, int> tileRowIndex,
        IReadOnlyDictionary<string, int> tilePoints,
        IReadOnlyDictionary<string, int> tileRequiredCount,
        IReadOnlyDictionary<string, List<TileActivityRuleSnapshotDto>> tileToRules)
    {
        var list = new List<string>();
        foreach (var kv in state.TileProgress)
        {
            var tileKey = kv.Key;
            if (state.CompletedTiles.Contains(tileKey))
                continue;
            if (!state.UnlockedRowIndices.Contains(tileRowIndex[tileKey]))
                continue;
            if (!tileToRules.TryGetValue(tileKey, out var rules))
                continue;
            if (rules.Any(r => r.AcceptedDropKeys.Contains(dropKey, StringComparer.Ordinal)))
                list.Add(tileKey);
        }
        return list;
    }

    private static OutcomeSnapshotDto? RollOutcome(AttemptSnapshotDto attempt, Random rng)
    {
        if (attempt.Outcomes.Count == 0)
            return null;
        var totalWeight = attempt.Outcomes.Sum(o => o.WeightNumerator);
        if (totalWeight <= 0)
            return attempt.Outcomes[0];
        var roll = rng.Next(0, totalWeight);
        var sum = 0;
        foreach (var o in attempt.Outcomes)
        {
            sum += o.WeightNumerator;
            if (roll < sum)
                return o;
        }
        return attempt.Outcomes[^1];
    }

    private static int SampleAttemptDuration(EventSnapshotDto snapshot, Guid activityId, string attemptKey, decimal skillMultiplier, Random rng)
    {
        var activity = snapshot.ActivitiesById.GetValueOrDefault(activityId);
        if (activity is null)
            return 60;
        var attempt = activity.Attempts.FirstOrDefault(a => a.Key == attemptKey) ?? activity.Attempts[0];
        var baseline = attempt.BaselineTimeSeconds;
        var variance = attempt.VarianceSeconds ?? 0;
        var time = baseline + (variance > 0 ? rng.Next(-variance, variance + 1) : 0);
        time = Math.Max(1, (int)(time * (double)skillMultiplier));
        return time;
    }

    private sealed class SimEvent
    {
        public int SimTime { get; }
        public int TeamIndex { get; }
        public int PlayerIndex { get; }
        public Guid ActivityId { get; }
        public string AttemptKey { get; }

        public SimEvent(int simTime, int teamIndex, int playerIndex, Guid activityId, string attemptKey)
        {
            SimTime = simTime;
            TeamIndex = teamIndex;
            PlayerIndex = playerIndex;
            ActivityId = activityId;
            AttemptKey = attemptKey;
        }
    }
}
