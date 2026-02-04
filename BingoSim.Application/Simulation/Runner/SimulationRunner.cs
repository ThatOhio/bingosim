using System.Collections.Generic;
using System.Text.Json;
using BingoSim.Application.Simulation.Allocation;
using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;
using Microsoft.Extensions.Logging;

namespace BingoSim.Application.Simulation.Runner;

/// <summary>
/// Executes one simulation run from a snapshot and produces per-team results.
/// Uses deterministic RNG from run seed string for reproducibility.
/// Supports group formation for group-capable activities, PerGroup/PerPlayer roll scopes, and GroupScalingBands.
/// </summary>
public class SimulationRunner(ITeamStrategyFactory strategyFactory, ILogger<SimulationRunner>? logger = null)
{
    private const int PerPlayerRollScope = 0;
    private const int PerGroupRollScope = 1;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null, WriteIndented = false };

    /// <summary>
    /// Runs simulation and returns per-team results (aggregates + timeline JSON).
    /// </summary>
    public IReadOnlyList<TeamRunResultDto> Execute(
        string snapshotJson,
        string runSeedString,
        CancellationToken cancellationToken = default,
        ISimulationProgressReporter? progressReporter = null)
    {
        var snapshot = EventSnapshotBuilder.Deserialize(snapshotJson);
        if (snapshot is null)
            throw new InvalidOperationException("Invalid snapshot JSON.");
        return Execute(snapshot, runSeedString, cancellationToken, progressReporter);
    }

    /// <summary>
    /// Runs simulation from a pre-parsed snapshot. Use when snapshot is already deserialized (e.g. cached per batch).
    /// </summary>
    public IReadOnlyList<TeamRunResultDto> Execute(
        EventSnapshotDto snapshot,
        string runSeedString,
        CancellationToken cancellationToken = default,
        ISimulationProgressReporter? progressReporter = null)
    {
        SnapshotValidator.Validate(snapshot);

        if (!DateTimeOffset.TryParse(snapshot.EventStartTimeEt, out var eventStartEt))
            throw new SnapshotValidationException($"EventStartTimeEt '{snapshot.EventStartTimeEt}' could not be parsed as DateTimeOffset.");

        var rngSeed = SeedDerivation.DeriveRngSeed(runSeedString, 0);
        var rng = new Random(rngSeed);

        var durationSeconds = snapshot.DurationSeconds;
        var rows = snapshot.Rows.OrderBy(r => r.Index).ToList();

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
                snapshot.UnlockPointsRequiredPerRow);
            teamStates.Add(state);
        }

        var playerCapabilitySets = BuildPlayerCapabilitySets(snapshot);
        var scheduleWindows = BuildScheduleWindowsCache(snapshot);
        var eventQueue = new PriorityQueue<SimEvent, (int EndTime, int TeamIndex, int FirstPlayerIndex)>();
        var grantsBuffer = new List<ProgressGrantSnapshotDto>();
        var groupCapsBuffer = new HashSet<string>(StringComparer.Ordinal);
        var simTime = 0;
        var lastFastForwardSimTime = (int?)null;
        var repeatCount = 0;
        const int MaxRepeatBeforeNoProgress = 10;

        // Initial schedule: form groups from all players per team
        for (var ti = 0; ti < snapshot.Teams.Count; ti++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var team = snapshot.Teams[ti];
            var state = teamStates[ti];
            var allPlayerIndices = Enumerable.Range(0, team.Players.Count).ToList();
            ScheduleEventsForPlayers(snapshot, team, ti, allPlayerIndices, state, playerCapabilitySets, scheduleWindows, groupCapsBuffer, tileRowIndex, tilePoints, tileRequiredCount, tileToRules, eventQueue, simTime, durationSeconds, rng, eventStartEt, cancellationToken);
        }

        while (simTime <= durationSeconds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (eventQueue.Count == 0)
            {
                var simTimeEt = ScheduleEvaluator.SimTimeToEt(eventStartEt, simTime);
                var nextEt = ScheduleEvaluator.GetEarliestNextSessionStart(snapshot, simTimeEt);
                if (nextEt is null)
                    break;
                var prevSimTime = simTime;
                var nextSimTime = ScheduleEvaluator.EtToSimTime(eventStartEt, nextEt.Value);

                // No-progress guard: nextSimTime must advance (catches schedule bugs)
                if (nextSimTime < prevSimTime)
                {
                    var onlineCount = CountOnlinePlayersAt(snapshot, scheduleWindows, simTimeEt);
                    throw new SimulationNoProgressException(
                        $"Schedule fast-forward: nextSimTime ({nextSimTime}) < simTime ({prevSimTime}). " +
                        $"simTimeEt={simTimeEt:o}, nextSimTimeEt={nextEt:o}, onlinePlayers={onlineCount}.",
                        prevSimTime,
                        nextSimTime,
                        simTimeEt.ToString("o"),
                        nextEt.Value.ToString("o"),
                        onlineCount);
                }

                // EtToSimTime truncates; when nextEt is <1s ahead, nextSimTime can equal prevSimTime. Advance by 1 to avoid loop.
                if (nextSimTime == prevSimTime)
                    nextSimTime = prevSimTime + 1;

                // No-progress guard: same nextSimTime repeated too many times (catches infinite loops)
                if (lastFastForwardSimTime == nextSimTime)
                {
                    repeatCount++;
                    if (repeatCount >= MaxRepeatBeforeNoProgress)
                    {
                        var onlineCount = CountOnlinePlayersAt(snapshot, scheduleWindows, simTimeEt);
                        throw new SimulationNoProgressException(
                            $"Schedule fast-forward: nextSimTime ({nextSimTime}) repeated {repeatCount} times. " +
                            $"simTimeEt={simTimeEt:o}, nextSimTimeEt={nextEt:o}, onlinePlayers={onlineCount}.",
                            prevSimTime,
                            nextSimTime,
                            simTimeEt.ToString("o"),
                            nextEt.Value.ToString("o"),
                            onlineCount);
                    }
                }
                else
                {
                    lastFastForwardSimTime = nextSimTime;
                    repeatCount = 0;
                }

                simTime = nextSimTime;
                logger?.LogDebug("Schedule fast-forward: simTime {Prev} -> {Next} (next session start)", prevSimTime, simTime);
                progressReporter?.Report(simTime, simTime, eventQueue.Count, CountOnlinePlayersAt(snapshot, scheduleWindows, ScheduleEvaluator.SimTimeToEt(eventStartEt, simTime)));

                if (simTime > durationSeconds)
                    break;
                for (var tIdx = 0; tIdx < snapshot.Teams.Count; tIdx++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var t = snapshot.Teams[tIdx];
                    var st = teamStates[tIdx];
                    var allPlayerIndices = Enumerable.Range(0, t.Players.Count).ToList();
                    ScheduleEventsForPlayers(snapshot, t, tIdx, allPlayerIndices, st, playerCapabilitySets, scheduleWindows, groupCapsBuffer, tileRowIndex, tilePoints, tileRequiredCount, tileToRules, eventQueue, simTime, durationSeconds, rng, eventStartEt, cancellationToken);
                }
                continue;
            }

            if (!eventQueue.TryDequeue(out var evt, out var _))
                break;
            simTime = evt.SimTime;
            if (simTime > durationSeconds)
                break;

            progressReporter?.Report(simTime, null, eventQueue.Count, CountOnlinePlayersAt(snapshot, scheduleWindows, ScheduleEvaluator.SimTimeToEt(eventStartEt, simTime)));

            var ti = evt.TeamIndex;
            var team = snapshot.Teams[ti];
            var state = teamStates[ti];
            var activity = snapshot.ActivitiesById.GetValueOrDefault(evt.ActivityId);
            if (activity is null)
                continue;

            var rule = evt.Rule;
            var playerIndices = evt.PlayerIndices;
            var groupSize = playerIndices.Count;

            grantsBuffer.Clear();
            CollectGrantsFromAttempts(activity, rule, playerIndices, groupSize, ti, playerCapabilitySets, grantsBuffer, groupCapsBuffer, rng, cancellationToken);

            var strategy = strategyFactory.GetStrategy(team.StrategyKey);
            foreach (var grant in grantsBuffer)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var eligible = GetEligibleTileKeys(snapshot, state, grant.DropKey, tileRowIndex, tilePoints, tileRequiredCount, tileToRules);
                if (eligible.Count == 0)
                    continue;
                var context = new GrantAllocationContext
                {
                    UnlockedRowIndices = state.UnlockedRowIndices,
                    TileProgress = state.TileProgress,
                    TileRequiredCount = tileRequiredCount,
                    TileRowIndex = tileRowIndex,
                    TilePoints = tilePoints,
                    EligibleTileKeys = eligible
                };
                var target = strategy.SelectTargetTileForGrant(context);
                if (target is null)
                    continue;
                state.AddProgress(target, grant.Units, simTime, tileRequiredCount[target], tileRowIndex[target], tilePoints[target]);
            }

            ScheduleEventsForPlayers(snapshot, team, ti, playerIndices, state, playerCapabilitySets, scheduleWindows, groupCapsBuffer, tileRowIndex, tilePoints, tileRequiredCount, tileToRules, eventQueue, simTime, durationSeconds, rng, eventStartEt, cancellationToken);
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

    private static void CollectGrantsFromAttempts(
        ActivitySnapshotDto activity,
        TileActivityRuleSnapshotDto rule,
        IReadOnlyList<int> playerIndices,
        int groupSize,
        int teamIndex,
        List<List<HashSet<string>>> playerCapabilitySets,
        List<ProgressGrantSnapshotDto> grantsOut,
        HashSet<string> groupCapsBuffer,
        Random rng,
        CancellationToken cancellationToken = default)
    {
        foreach (var attempt in activity.Attempts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (attempt.RollScope == PerPlayerRollScope)
            {
                foreach (var pi in playerIndices)
                {
                    var caps = playerCapabilitySets[teamIndex][pi];
                    var (_, effectiveProb) = GroupScalingBandSelector.ComputeEffectiveMultipliers(
                        activity.GroupScalingBands, groupSize, rule, caps);
                    var outcome = RollOutcome(attempt, rule, effectiveProb, rng);
                    if (outcome is { } o && o.Grants.Count > 0)
                        grantsOut.AddRange(o.Grants);
                }
            }
            else
            {
                GetUnionCapabilityKeys(playerCapabilitySets[teamIndex], playerIndices, groupCapsBuffer);
                var (_, effectiveProb) = GroupScalingBandSelector.ComputeEffectiveMultipliers(
                    activity.GroupScalingBands, groupSize, rule, groupCapsBuffer);
                var outcome = RollOutcome(attempt, rule, effectiveProb, rng);
                if (outcome is { } o && o.Grants.Count > 0)
                    grantsOut.AddRange(o.Grants);
            }
        }
    }

    private static void GetUnionCapabilityKeys(List<HashSet<string>> teamCaps, IReadOnlyList<int> playerIndices, HashSet<string> buffer)
    {
        buffer.Clear();
        foreach (var pi in playerIndices)
        {
            if (pi < teamCaps.Count)
            {
                foreach (var cap in teamCaps[pi])
                    buffer.Add(cap);
            }
        }
    }

    private void ScheduleEventsForPlayers(
        EventSnapshotDto snapshot,
        TeamSnapshotDto team,
        int teamIndex,
        IReadOnlyList<int> playerIndicesToSchedule,
        TeamRunState state,
        List<List<HashSet<string>>> playerCapabilitySets,
        List<List<ScheduleEvaluator.DailyWindows>> scheduleWindows,
        HashSet<string> groupCapsBuffer,
        IReadOnlyDictionary<string, int> tileRowIndex,
        IReadOnlyDictionary<string, int> tilePoints,
        IReadOnlyDictionary<string, int> tileRequiredCount,
        IReadOnlyDictionary<string, List<TileActivityRuleSnapshotDto>> tileToRules,
        PriorityQueue<SimEvent, (int EndTime, int TeamIndex, int FirstPlayerIndex)> eventQueue,
        int simTime,
        int durationSeconds,
        Random rng,
        DateTimeOffset eventStartEt,
        CancellationToken cancellationToken = default)
    {
        if (playerIndicesToSchedule.Count == 0)
            return;

        cancellationToken.ThrowIfCancellationRequested();

        var simTimeEt = ScheduleEvaluator.SimTimeToEt(eventStartEt, simTime);
        var teamWindows = scheduleWindows[teamIndex];
        var scheduleFiltered = playerIndicesToSchedule
            .Where(pi => pi < teamWindows.Count && ScheduleEvaluator.IsOnlineAt(teamWindows[pi], simTimeEt))
            .ToList();

        if (scheduleFiltered.Count == 0)
            return;

        var sortedPlayers = scheduleFiltered
            .OrderBy(pi => team.Players[pi].PlayerId)
            .ToList();

        var strategy = strategyFactory.GetStrategy(team.StrategyKey);
        var assignments = new List<(int pi, Guid activityId, TileActivityRuleSnapshotDto rule)>();
        foreach (var pi in sortedPlayers)
        {
            var caps = playerCapabilitySets[teamIndex][pi];
            var (activityId, rule) = GetTaskForPlayer(strategy, snapshot, team, pi, state, caps, tileRowIndex, tilePoints, tileRequiredCount, tileToRules);
            if (activityId is null || rule is null)
                continue;
            assignments.Add((pi, activityId.Value, rule));
        }

        var used = new HashSet<int>();
        foreach (var (pi, activityId, rule) in assignments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (used.Contains(pi))
                continue;

            if (!snapshot.ActivitiesById.TryGetValue(activityId, out var activity))
                continue;

            var modeSupport = activity.ModeSupport;
            var supportsGroup = modeSupport.SupportsGroup;
            var supportsSolo = modeSupport.SupportsSolo;
            var minGroupSize = modeSupport.MinGroupSize ?? 1;
            var maxGroupSize = modeSupport.MaxGroupSize ?? int.MaxValue;

            var sameWork = assignments
                .Where(a => a.activityId == activityId && a.rule == rule && !used.Contains(a.pi))
                .Select(a => a.pi)
                .OrderBy(p => team.Players[p].PlayerId)
                .ToList();

            List<int> group;
            if (supportsGroup && sameWork.Count >= 2)
            {
                var desiredSize = Math.Min(sameWork.Count, maxGroupSize);
                group = sameWork.Take(desiredSize).ToList();
                if (group.Count < minGroupSize && !supportsSolo)
                    continue;
                if (group.Count == 1 && !supportsSolo)
                    continue;
            }
            else if (supportsSolo)
            {
                group = [sameWork[0]];
            }
            else if (supportsGroup && sameWork.Count >= minGroupSize)
            {
                group = sameWork.Take(Math.Min(sameWork.Count, maxGroupSize)).ToList();
            }
            else
            {
                continue;
            }

            foreach (var p in group)
                used.Add(p);

            var duration = SampleAttemptDuration(snapshot, activityId, group, rule, playerCapabilitySets[teamIndex], teamIndex, groupCapsBuffer, rng);
            var attemptEndSimTime = simTime + duration;

            {
                var attemptEndEt = ScheduleEvaluator.SimTimeToEt(eventStartEt, attemptEndSimTime);
                var minSessionEnd = (DateTimeOffset?)null;
                foreach (var p in group)
                {
                    var sessionEnd = p < teamWindows.Count ? ScheduleEvaluator.GetCurrentSessionEnd(teamWindows[p], simTimeEt) : null;
                    if (sessionEnd is null)
                        continue;
                    if (minSessionEnd is null || sessionEnd < minSessionEnd)
                        minSessionEnd = sessionEnd;
                }
                if (minSessionEnd is { } end && attemptEndEt >= end)
                {
                    logger?.LogDebug("Schedule: attempt skipped (would end at/past session end)");
                    continue;
                }
            }

            var endTime = Math.Min(attemptEndSimTime, durationSeconds + 1);
            var priority = (endTime, teamIndex, group[0]);
            eventQueue.Enqueue(new SimEvent(endTime, teamIndex, group, activityId, rule), priority);
        }
    }

    private static int CountOnlinePlayersAt(
        EventSnapshotDto snapshot,
        List<List<ScheduleEvaluator.DailyWindows>> scheduleWindows,
        DateTimeOffset simTimeEt)
    {
        var count = 0;
        for (var ti = 0; ti < snapshot.Teams.Count; ti++)
        {
            var teamWindows = scheduleWindows[ti];
            for (var pi = 0; pi < teamWindows.Count; pi++)
            {
                if (ScheduleEvaluator.IsOnlineAt(teamWindows[pi], simTimeEt))
                    count++;
            }
        }
        return count;
    }

    private static List<List<HashSet<string>>> BuildPlayerCapabilitySets(EventSnapshotDto snapshot)
    {
        var result = new List<List<HashSet<string>>>();
        foreach (var team in snapshot.Teams)
        {
            var sets = new List<HashSet<string>>();
            foreach (var player in team.Players)
                sets.Add(new HashSet<string>(player.CapabilityKeys, StringComparer.Ordinal));
            result.Add(sets);
        }
        return result;
    }

    private static List<List<ScheduleEvaluator.DailyWindows>> BuildScheduleWindowsCache(EventSnapshotDto snapshot)
    {
        var result = new List<List<ScheduleEvaluator.DailyWindows>>();
        foreach (var team in snapshot.Teams)
        {
            var windows = new List<ScheduleEvaluator.DailyWindows>();
            foreach (var player in team.Players)
                windows.Add(ScheduleEvaluator.DailyWindows.Build(player.Schedule));
            result.Add(windows);
        }
        return result;
    }

    private static (Guid? activityId, TileActivityRuleSnapshotDto? rule) GetTaskForPlayer(
        ITeamStrategy strategy,
        EventSnapshotDto snapshot,
        TeamSnapshotDto team,
        int playerIndex,
        TeamRunState state,
        HashSet<string> playerCapabilities,
        IReadOnlyDictionary<string, int> tileRowIndex,
        IReadOnlyDictionary<string, int> tilePoints,
        IReadOnlyDictionary<string, int> tileRequiredCount,
        IReadOnlyDictionary<string, List<TileActivityRuleSnapshotDto>> tileToRules)
    {
        var context = BuildTaskSelectionContext(snapshot, team, playerIndex, state, playerCapabilities, tileRowIndex, tilePoints, tileRequiredCount, tileToRules);
        var result = strategy.SelectTaskForPlayer(context);
        return result is { } r ? (r.activityId, r.rule) : (null, null);
    }

    private static TaskSelectionContext BuildTaskSelectionContext(
        EventSnapshotDto snapshot,
        TeamSnapshotDto team,
        int playerIndex,
        TeamRunState state,
        HashSet<string> playerCapabilities,
        IReadOnlyDictionary<string, int> tileRowIndex,
        IReadOnlyDictionary<string, int> tilePoints,
        IReadOnlyDictionary<string, int> tileRequiredCount,
        IReadOnlyDictionary<string, List<TileActivityRuleSnapshotDto>> tileToRules)
    {
        var tileToRulesReadOnly = tileToRules.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<TileActivityRuleSnapshotDto>)kv.Value,
            StringComparer.Ordinal);

        return new TaskSelectionContext
        {
            PlayerIndex = playerIndex,
            PlayerCapabilities = playerCapabilities,
            EventSnapshot = snapshot,
            TeamSnapshot = team,
            UnlockedRowIndices = state.UnlockedRowIndices,
            TileProgress = state.TileProgress,
            TileRequiredCount = tileRequiredCount,
            CompletedTiles = state.CompletedTiles,
            TileRowIndex = tileRowIndex,
            TilePoints = tilePoints,
            TileToRules = tileToRulesReadOnly
        };
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
        foreach (var tileKey in tileToRules.Keys)
        {
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

    /// <param name="effectiveProbabilityMultiplier">groupProb * modifierProb (from ComputeEffectiveMultipliers).</param>
    private static OutcomeSnapshotDto? RollOutcome(
        AttemptSnapshotDto attempt,
        TileActivityRuleSnapshotDto rule,
        decimal effectiveProbabilityMultiplier,
        Random rng)
    {
        if (attempt.Outcomes.Count == 0)
            return null;

        var weights = ModifierApplicator.ApplyProbabilityMultiplier(attempt.Outcomes, rule.AcceptedDropKeys, effectiveProbabilityMultiplier);

        var totalWeight = weights.Sum();
        if (totalWeight <= 0)
            return attempt.Outcomes[0];
        var roll = rng.Next(0, totalWeight);
        var sum = 0;
        for (var i = 0; i < attempt.Outcomes.Count; i++)
        {
            sum += weights[i];
            if (roll < sum)
                return attempt.Outcomes[i];
        }
        return attempt.Outcomes[^1];
    }

    private static int SampleAttemptDuration(
        EventSnapshotDto snapshot,
        Guid activityId,
        IReadOnlyList<int> playerIndices,
        TileActivityRuleSnapshotDto rule,
        List<HashSet<string>> teamCapabilitySets,
        int teamIndex,
        HashSet<string> groupCapsBuffer,
        Random rng)
    {
        if (!snapshot.ActivitiesById.TryGetValue(activityId, out var activity))
            throw new InvalidOperationException($"Activity {activityId} not found in snapshot.");

        var (baseline, variance) = GetMaxAttemptTimeModel(activity);
        var rawTime = baseline + (variance > 0 ? rng.Next(-variance, variance + 1) : 0);
        rawTime = Math.Max(1, rawTime);

        var team = snapshot.Teams[teamIndex];
        var groupSize = playerIndices.Count;

        // v1 assumption: slowest member dominates — the group waits for the slowest player.
        // Higher SkillTimeMultiplier = slower. We use max (slowest) so the attempt duration reflects the bottleneck.
        var skillMultiplier = playerIndices.Count > 0
            ? playerIndices.Max(pi => pi < team.Players.Count ? team.Players[pi].SkillTimeMultiplier : 1.0m)
            : 1.0m;

        GetUnionCapabilityKeys(teamCapabilitySets, playerIndices, groupCapsBuffer);
        var (effectiveTimeMult, _) = GroupScalingBandSelector.ComputeEffectiveMultipliers(
            activity.GroupScalingBands, groupSize, rule, groupCapsBuffer);

        var time = rawTime * (double)skillMultiplier * (double)effectiveTimeMult;
        return Math.Max(1, (int)Math.Floor(time));
    }

    private static (int baseline, int variance) GetMaxAttemptTimeModel(ActivitySnapshotDto activity)
    {
        if (activity.Attempts.Count == 0)
            throw new InvalidOperationException($"Activity '{activity.Key}' has no Attempts.");
        var maxBaseline = activity.Attempts.Max(a => a.BaselineTimeSeconds);
        var maxVariance = activity.Attempts.Max(a => a.VarianceSeconds);
        return (maxBaseline, maxVariance);
    }

    private sealed class SimEvent
    {
        public int SimTime { get; }
        public int TeamIndex { get; }
        public IReadOnlyList<int> PlayerIndices { get; }
        public Guid ActivityId { get; }
        public TileActivityRuleSnapshotDto Rule { get; }

        public SimEvent(int simTime, int teamIndex, IReadOnlyList<int> playerIndices, Guid activityId, TileActivityRuleSnapshotDto rule)
        {
            SimTime = simTime;
            TeamIndex = teamIndex;
            PlayerIndices = playerIndices;
            ActivityId = activityId;
            Rule = rule;
        }
    }
}
