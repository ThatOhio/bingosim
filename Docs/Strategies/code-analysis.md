# Code Analysis: Strategies, Tiles, Activities, and Player Capabilities

This document provides a detailed technical analysis of the BingoSim codebase to support the design and implementation of the "Row Unlocking Strategy." It covers strategy architecture, tile/row mechanics, activity/time calculations, player capabilities, multi-player tile work, and current state access.

---

## 1. Strategy Architecture

### 1.1 Strategy Types (Catalog)

Strategies are identified by string keys, not an enum. The catalog lives in:

**File:** `BingoSim.Application/StrategyKeys/StrategyCatalog.cs`

```csharp
public static class StrategyCatalog
{
    public const string RowRush = "RowRush";
    public const string GreedyPoints = "GreedyPoints";

    private static readonly string[] AllKeys = [RowRush, GreedyPoints];

    public static IReadOnlyList<string> GetSupportedKeys() => AllKeys;
    public static bool IsSupported(string key) =>
        !string.IsNullOrWhiteSpace(key) && AllKeys.Contains(key.Trim(), StringComparer.Ordinal);
}
```

### 1.2 Strategy Storage (Per Team)

**File:** `BingoSim.Core/Entities/StrategyConfig.cs`

Each team has a 1:1 `StrategyConfig` with:
- `StrategyKey` (string): e.g. `"RowRush"`, `"GreedyPoints"`
- `ParamsJson` (string?): optional JSON for future strategy parameters

### 1.3 Base Strategy Interface

**File:** `BingoSim.Application/Simulation/Allocation/IProgressAllocator.cs`

```csharp
public interface IProgressAllocator
{
    /// <summary>
    /// Returns the tile key that should receive the full grant (single tile in v1).
    /// If no eligible tile, returns null (grant is dropped).
    /// </summary>
    string? SelectTargetTile(AllocatorContext context);
}
```

**Important:** The strategy is a **progress allocator**, not a tile selector. It decides **which tile receives a grant** when multiple tiles could accept the same drop. Task assignment (which tile a player works on) is fixed and shared by all strategies.

### 1.4 Allocator Context

**File:** `BingoSim.Application/Simulation/Allocation/AllocatorContext.cs`

```csharp
public sealed class AllocatorContext
{
    public required IReadOnlySet<int> UnlockedRowIndices { get; init; }
    public required IReadOnlyDictionary<string, int> TileProgress { get; init; }
    public required IReadOnlyDictionary<string, int> TileRequiredCount { get; init; }
    public required IReadOnlyDictionary<string, int> TileRowIndex { get; init; }
    public required IReadOnlyDictionary<string, int> TilePoints { get; init; }
    public required IReadOnlyList<string> EligibleTileKeys { get; init; }
}
```

### 1.5 Strategy Invocation

**File:** `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` (lines 201–223)

When an activity attempt completes and produces grants:

1. For each grant, `GetEligibleTileKeys` finds tiles that accept the `DropKey`, are in an unlocked row, and are not completed.
2. If `eligible.Count == 0`, the grant is dropped.
3. Otherwise, the allocator is obtained via `allocatorFactory.GetAllocator(team.StrategyKey)` and `SelectTargetTile(context)` is called.
4. The returned tile key receives the grant via `state.AddProgress(...)`.

```csharp
var allocator = allocatorFactory.GetAllocator(team.StrategyKey);
foreach (var grant in grantsBuffer)
{
    var eligible = GetEligibleTileKeys(snapshot, state, grant.DropKey, ...);
    if (eligible.Count == 0) continue;
    var context = new AllocatorContext { ... EligibleTileKeys = eligible };
    var target = allocator.SelectTargetTile(context);
    if (target is null) continue;
    state.AddProgress(target, grant.Units, simTime, ...);
}
```

### 1.6 Allocator Factory

**File:** `BingoSim.Application/Simulation/Allocation/ProgressAllocatorFactory.cs`

```csharp
public sealed class ProgressAllocatorFactory : IProgressAllocatorFactory
{
    private readonly IReadOnlyDictionary<string, IProgressAllocator> _allocators;

    public ProgressAllocatorFactory()
    {
        _allocators = new Dictionary<string, IProgressAllocator>(StringComparer.Ordinal)
        {
            [StrategyCatalog.RowRush] = new RowRushAllocator(),
            [StrategyCatalog.GreedyPoints] = new GreedyPointsAllocator()
        };
    }

    public IProgressAllocator GetAllocator(string strategyKey)
    {
        if (!string.IsNullOrWhiteSpace(strategyKey) && _allocators.TryGetValue(strategyKey.Trim(), out var allocator))
            return allocator;
        return _allocators[StrategyCatalog.RowRush];  // Default fallback
    }
}
```

### 1.7 Existing Allocator Implementations

**RowRushAllocator** — `BingoSim.Application/Simulation/Allocation/RowRushAllocator.cs`

```csharp
public string? SelectTargetTile(AllocatorContext context)
{
    if (context.EligibleTileKeys.Count == 0) return null;

    return context.EligibleTileKeys
        .OrderBy(key => context.TileRowIndex[key])
        .ThenBy(key => context.TilePoints[key])
        .ThenBy(key => key, StringComparer.Ordinal)
        .First();
}
```

Order: lowest row → lowest points (1,2,3,4) → tile key (alphabetical tie-break).

**GreedyPointsAllocator** — `BingoSim.Application/Simulation/Allocation/GreedyPointsAllocator.cs`

```csharp
public string? SelectTargetTile(AllocatorContext context)
{
    if (context.EligibleTileKeys.Count == 0) return null;

    return context.EligibleTileKeys
        .OrderByDescending(key => context.TilePoints[key])
        .ThenBy(key => context.TileRowIndex[key])
        .ThenBy(key => key, StringComparer.Ordinal)
        .First();
}
```

Order: highest points (4,3,2,1) → lowest row → tile key (alphabetical tie-break).

### 1.8 Task Assignment (Fixed, Not Strategy-Dependent)

**File:** `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` — `GetFirstEligibleActivity` (lines 349–374)

Task assignment is the same for all strategies. It picks the first incomplete tile the player is eligible for:

- Rows iterated in order (0, 1, 2, …)
- Within each row, tiles ordered by points ascending (1, 2, 3, 4)
- First tile with a rule whose requirements the player meets and that is in an unlocked row

```csharp
private static (Guid? activityId, TileActivityRuleSnapshotDto? rule) GetFirstEligibleActivity(
    EventSnapshotDto snapshot, TeamSnapshotDto team, int playerIndex,
    IReadOnlySet<int> unlockedRows, IReadOnlySet<string> completedTiles, HashSet<string> playerCaps)
{
    foreach (var row in snapshot.Rows.OrderBy(r => r.Index))
    {
        if (!unlockedRows.Contains(row.Index)) continue;
        foreach (var tile in row.Tiles.OrderBy(t => t.Points))
        {
            if (completedTiles.Contains(tile.Key)) continue;
            foreach (var rule in tile.AllowedActivities)
            {
                if (rule.RequirementKeys.Count > 0 && !rule.RequirementKeys.All(playerCaps.Contains))
                    continue;
                var activity = snapshot.ActivitiesById.GetValueOrDefault(rule.ActivityDefinitionId);
                if (activity is null || activity.Attempts.Count == 0) continue;
                return (rule.ActivityDefinitionId, rule);
            }
        }
    }
    return (null, null);
}
```

---

## 2. Tile and Row Mechanics

### 2.1 Tile Structure

**Domain:** `BingoSim.Core/ValueObjects/Tile.cs`

```csharp
public sealed record Tile
{
    public string Key { get; init; }           // Stable identifier within event
    public string Name { get; init; }
    public int Points { get; init; }           // 1, 2, 3, or 4
    public int RequiredCount { get; init; }    // Progress units needed to complete
    public List<TileActivityRule> AllowedActivities { get; private set; }
}
```

**Snapshot:** `BingoSim.Application/Simulation/Snapshot/TileSnapshotDto.cs`

```csharp
public sealed class TileSnapshotDto
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required int Points { get; init; }
    public required int RequiredCount { get; init; }
    public required List<TileActivityRuleSnapshotDto> AllowedActivities { get; init; }
}
```

### 2.2 Row Structure

**Domain:** `BingoSim.Core/ValueObjects/Row.cs`

```csharp
public sealed record Row
{
    public int Index { get; init; }            // 0-based
    public List<Tile> Tiles { get; private set; }  // Exactly 4 tiles
}
```

Each row must have exactly 4 tiles with points 1, 2, 3, 4.

**Snapshot:** `BingoSim.Application/Simulation/Snapshot/RowSnapshotDto.cs`

```csharp
public sealed class RowSnapshotDto
{
    public required int Index { get; init; }
    public required List<TileSnapshotDto> Tiles { get; init; }
}
```

### 2.3 Row Unlocking (5-Point Threshold)

**Rule:** Row N unlocks when the team has completed at least `UnlockPointsRequiredPerRow` points in row N-1. Row 0 is always unlocked at sim time 0.

**File:** `BingoSim.Application/Simulation/Runner/RowUnlockHelper.cs`

```csharp
public static IReadOnlySet<int> ComputeUnlockedRows(
    int unlockPointsRequiredPerRow,
    IReadOnlyDictionary<int, int> completedPointsByRow,
    int totalRowCount)
{
    var unlocked = new HashSet<int> { 0 };
    for (var row = 1; row < totalRowCount; row++)
    {
        var prevRow = row - 1;
        var completedInPrev = completedPointsByRow.GetValueOrDefault(prevRow, 0);
        if (completedInPrev >= unlockPointsRequiredPerRow)
            unlocked.Add(row);
    }
    return unlocked;
}
```

**File:** `BingoSim.Application/Simulation/Runner/TeamRunState.cs` — `UnlockedRowIndices` (lines 22–41)

`TeamRunState` maintains `_completedPointsByRow` and recomputes unlocked rows when dirty:

```csharp
public IReadOnlySet<int> UnlockedRowIndices
{
    get
    {
        if (_unlockedRowsDirty)
        {
            _unlockedRowIndices.Clear();
            _unlockedRowIndices.Add(0);
            for (var row = 1; row < TotalRowCount; row++)
            {
                var prevRow = row - 1;
                var completedInPrev = _completedPointsByRow.GetValueOrDefault(prevRow, 0);
                if (completedInPrev >= UnlockPointsRequiredPerRow)
                    _unlockedRowIndices.Add(row);
            }
            _unlockedRowsDirty = false;
        }
        return _unlockedRowIndices;
    }
}
```

### 2.4 Tile Points and Storage

- **Points** are stored on each tile (1–4).
- **Completed points per row** are tracked in `TeamRunState._completedPointsByRow[rowIndex]` as the sum of points of completed tiles in that row.
- When a tile completes, `AddProgress` updates: `_completedPointsByRow[row] += points`.

### 2.5 Current vs Next Row to Unlock

- **Current unlocked rows:** `state.UnlockedRowIndices` (e.g. `{0, 1, 2}`).
- **Next row to unlock:** The first row index not in `UnlockedRowIndices`. If `UnlockedRowIndices.Max()` is `N`, the next row is `N + 1`.
- **Points needed for next row:** `UnlockPointsRequiredPerRow - _completedPointsByRow[UnlockedRowIndices.Max()]` (for the row that gates the next unlock).

### 2.6 Getting All Tiles for a Row

**From snapshot:**

```csharp
var row = snapshot.Rows.FirstOrDefault(r => r.Index == rowIndex);
var tiles = row?.Tiles ?? [];
```

**From dictionaries built at run start** (in `SimulationRunner.Execute`):

```csharp
// tileRowIndex[tileKey] = row index
// tilePoints[tileKey] = points
var tilesInRow = tileRowIndex.Where(kv => kv.Value == rowIndex).Select(kv => kv.Key).ToList();
```

---

## 3. Activity and Time Calculations

### 3.1 Activity Structure

**Domain:** `BingoSim.Core/Entities/ActivityDefinition` (via EF configuration)

- `Key`, `Name`
- `ModeSupport`: solo/group, min/max group size
- `Attempts`: list of `ActivityAttemptDefinition`
- `GroupScalingBands`: list of `GroupSizeBand`

**Snapshot:** `BingoSim.Application/Simulation/Snapshot/ActivitySnapshotDto.cs`

```csharp
public sealed class ActivitySnapshotDto
{
    public required Guid Id { get; init; }
    public required string Key { get; init; }
    public required List<AttemptSnapshotDto> Attempts { get; init; }
    public required List<GroupSizeBandSnapshotDto> GroupScalingBands { get; init; }
    public required ActivityModeSupportSnapshotDto ModeSupport { get; init; }
}
```

### 3.2 Attempt and Outcome Structure

**Attempt:** `BingoSim.Core/ValueObjects/ActivityAttemptDefinition.cs`

```csharp
public sealed record ActivityAttemptDefinition
{
    public string Key { get; init; }
    public RollScope RollScope { get; init; }      // PerPlayer = 0, PerGroup = 1
    public AttemptTimeModel TimeModel { get; init; }
    public List<ActivityOutcomeDefinition> Outcomes { get; private set; }
}
```

**Time model:** `BingoSim.Core/ValueObjects/AttemptTimeModel.cs`

```csharp
public sealed record AttemptTimeModel
{
    public int BaselineTimeSeconds { get; init; }
    public TimeDistribution Distribution { get; init; }  // Uniform, NormalApprox, Custom
    public int? VarianceSeconds { get; init; }
}
```

**Outcome:** `BingoSim.Core/ValueObjects/ActivityOutcomeDefinition.cs`

```csharp
public sealed record ActivityOutcomeDefinition
{
    public string Key { get; init; }
    public int WeightNumerator { get; init; }
    public int WeightDenominator { get; init; }
    public List<ProgressGrant> Grants { get; private set; }
}
```

**Grant:** `BingoSim.Core/ValueObjects/ProgressGrant.cs`

```csharp
public sealed record ProgressGrant
{
    public string DropKey { get; init; }
    public int Units { get; init; }
}
```

### 3.3 Grant System (Drop Rates, Attempts, Time)

- **Drop rates:** Each outcome has `WeightNumerator`/`WeightDenominator`. A weighted random roll selects an outcome; its `Grants` are produced.
- **Attempts:** An activity can have multiple `ActivityAttemptDefinition` (e.g. multiple loot lines). All are rolled when the attempt completes.
- **Time per attempt:** Sampled from `AttemptTimeModel` (baseline ± variance). For multi-attempt activities, the **maximum** baseline and variance across attempts are used.

### 3.4 Activity Time Configuration Storage

- **Domain:** `ActivityDefinition.Attempts[].TimeModel` (BaselineTimeSeconds, VarianceSeconds, Distribution)
- **Snapshot:** `AttemptSnapshotDto.BaselineTimeSeconds`, `AttemptSnapshotDto.VarianceSeconds`

### 3.5 Formula for Attempt Duration

**File:** `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` — `SampleAttemptDuration` (lines 403–584)

```
rawTime = baseline + Random(-variance, variance+1)
rawTime = max(1, rawTime)

effectiveTime = rawTime * skillMultiplier * effectiveTimeMult
duration = max(1, floor(effectiveTime))
```

Where:
- `skillMultiplier` = max of group members’ `SkillTimeMultiplier` (slowest player dominates)
- `effectiveTimeMult` = `GroupScalingBandSelector` time multiplier × `ModifierApplicator` time multiplier (from capability-based modifiers)

```csharp
var (baseline, variance) = GetMaxAttemptTimeModel(activity);
var rawTime = baseline + (variance > 0 ? rng.Next(-variance, variance + 1) : 0);
rawTime = Math.Max(1, rawTime);

var skillMultiplier = playerIndices.Count > 0
    ? playerIndices.Max(pi => team.Players[pi].SkillTimeMultiplier)
    : 1.0m;

var (effectiveTimeMult, _) = GroupScalingBandSelector.ComputeEffectiveMultipliers(
    activity.GroupScalingBands, groupSize, rule, groupCapsBuffer);

var time = rawTime * (double)skillMultiplier * (double)effectiveTimeMult;
return Math.Max(1, (int)Math.Floor(time));
```

### 3.6 Tile Completion Time

A tile’s completion time is the sim time at which it receives enough progress to reach `RequiredCount`. Stored in `TeamRunState._tileCompletionTimes[tileKey]`.

---

## 4. Player Capabilities and Requirements

### 4.1 Player Capability System

**Domain:** `BingoSim.Core/Entities/PlayerProfile.cs`

```csharp
private readonly List<Capability> _capabilities = [];
public IReadOnlyList<Capability> Capabilities => _capabilities.AsReadOnly();

public bool HasCapability(string key) => _capabilities.Any(c => c.Key == key);
```

**Capability:** `BingoSim.Core/ValueObjects/Capability.cs`

```csharp
public sealed record Capability
{
    public string Key { get; init; }   // e.g. "quest.ds2", "item.dragon_hunter_lance"
    public string Name { get; init; }  // Display name
}
```

### 4.2 Requirement Storage

**Domain:** `TileActivityRule.Requirements` is `List<Capability>`.

**Snapshot:** `TileActivityRuleSnapshotDto.RequirementKeys` is `List<string>` (capability keys only).

**File:** `BingoSim.Application/Simulation/Snapshot/EventSnapshotBuilder.cs` (line 69)

```csharp
RequirementKeys = rule.Requirements.Select(req => req.Key).ToList(),
```

Requirements are stored as a **list of capability keys**, not comma-separated. Each `TileActivityRule` has `RequirementKeys` (snapshot) or `Requirements` (domain).

### 4.3 Eligibility Check

**File:** `BingoSim.Application/Simulation/Runner/SimulationRunner.cs` (lines 363–364)

```csharp
if (rule.RequirementKeys.Count > 0 && !rule.RequirementKeys.All(playerCaps.Contains))
    continue;
```

A player can work on a tile via a rule only if they have **all** required capability keys. `playerCaps` is a `HashSet<string>` built from `PlayerSnapshotDto.CapabilityKeys`.

### 4.4 Capability Key Examples

From `DevSeedService.cs`:

| Key | Name |
|-----|------|
| `quest.ds2` | Dragon Slayer II |
| `item.dragon_hunter_lance` | Dragon Hunter Lance |
| `quest.sote` | Song of the Elves |

### 4.5 Requirement Configuration Examples

From `DevSeedService.GetRequirementsForTile`:

```csharp
if (actKey is "boss.vorkath" or "boss.zulrah")
    return rowIdx % 3 == 0 ? [questDs2] : [];
if (actKey is "raid.cox" or "raid.toa")
    return rowIdx % 4 != 2 ? [questSote] : [];
return [];
```

### 4.6 Modifiers (Optional Capability-Based Bonuses)

`TileActivityRule` also has `Modifiers` (`ActivityModifierRule`). If a player has the capability, they get:
- `TimeMultiplier`: faster attempts
- `ProbabilityMultiplier`: better drop chance

---

## 5. Multi-Player Tile Work

### 5.1 Multiple Players on Same Tile

When multiple players are assigned the same (activity, rule) pair, they can form a group and run the activity together. Group formation happens in `ScheduleEventsForPlayers`:

```csharp
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
    // ...
}
```

### 5.2 Group-Based Activity Logic

- **Solo:** `ModeSupport.SupportsSolo` — one player per attempt.
- **Group:** `ModeSupport.SupportsGroup` — 2+ players can form a group; `MinGroupSize`/`MaxGroupSize` constrain size.
- If both support solo and group, solo is used when only one player is on that tile; otherwise a group is formed.

### 5.3 Tile Progress with Multiple Players

Progress is **team-level**, not per player. When a group completes an attempt:

1. **PerPlayer** rolls: each player rolls; each roll can produce grants.
2. **PerGroup** rolls: one roll for the group; grants go to the team.
3. All grants are allocated via the allocator to a single tile (or dropped if no eligible tile).
4. `state.AddProgress(tileKey, units, simTime, ...)` updates team progress.

So multiple players working on the same tile contribute to the same team progress pool. The allocator decides which tile gets each grant when several tiles could accept it.

### 5.4 Group Scaling

**File:** `BingoSim.Application/Simulation/GroupScalingBandSelector.cs`

`GroupSizeBand` defines `TimeMultiplier` and `ProbabilityMultiplier` for a group size range. The first band where `MinSize <= groupSize <= MaxSize` is used.

---

## 6. Current State Access

### 6.1 Simulation State

During a run, per-team state is in `TeamRunState`:

```csharp
// TeamRunState (internal to SimulationRunner)
public IReadOnlySet<int> UnlockedRowIndices { get; }
public IReadOnlyDictionary<string, int> TileProgress { get; }
public IReadOnlySet<string> CompletedTiles { get; }
public IReadOnlyDictionary<int, int> RowUnlockTimes { get; }
public IReadOnlyDictionary<string, int> TileCompletionTimes { get; }
public int TotalPoints { get; }
public int TilesCompletedCount { get; }
public int RowReached { get; }
```

- `TileProgress`: current progress units for incomplete tiles.
- `CompletedTiles`: tile keys that are done.
- `RowUnlockTimes`: sim time when each row unlocked.
- `TileCompletionTimes`: sim time when each tile completed.

### 6.2 Snapshot Access

The simulation runs from `EventSnapshotDto`:

```csharp
var snapshot = EventSnapshotBuilder.Deserialize(snapshotJson);
// or
var snapshot = await eventSnapshotBuilder.BuildSnapshotAsync(...);
```

Snapshot contains:
- `Rows` (ordered by Index)
- `ActivitiesById`
- `Teams` (with `StrategyKey`, `Players`, etc.)
- `UnlockPointsRequiredPerRow`
- `DurationSeconds`
- `EventStartTimeEt`

### 6.3 Available Tiles for a Player

A tile is available if:
1. Its row is in `state.UnlockedRowIndices`
2. It is not in `state.CompletedTiles`
3. The player has at least one `TileActivityRule` whose `RequirementKeys` are all in the player’s capability set

Logic is in `GetFirstEligibleActivity` (iterating rows → tiles → rules).

### 6.4 Tile Completion Status and Progress

- **Completed:** `state.CompletedTiles.Contains(tileKey)`
- **In progress:** `state.TileProgress.TryGetValue(tileKey, out var progress)` — `progress` is current units; compare to `tileRequiredCount[tileKey]` for completion.
- **Completion time:** `state.TileCompletionTimes.TryGetValue(tileKey, out var simTime)`

### 6.5 Post-Run Results

**File:** `TeamRunResultDto` (returned by `SimulationRunner.Execute`)

- `TotalPoints`, `TilesCompletedCount`, `RowReached`
- `RowUnlockTimesJson`, `TileCompletionTimesJson` (JSON-serialized dictionaries)

---

## Summary: Key Entry Points for Row Unlocking Strategy

| Concern | Location |
|---------|----------|
| Add new strategy | `StrategyCatalog`, `ProgressAllocatorFactory`, new `IProgressAllocator` |
| Allocator context | `AllocatorContext` — has `UnlockedRowIndices`, `TileProgress`, `TilePoints`, `TileRowIndex`, `EligibleTileKeys` |
| Row unlock logic | `RowUnlockHelper.ComputeUnlockedRows`, `TeamRunState.UnlockedRowIndices` |
| Task assignment | `GetFirstEligibleActivity` — fixed; strategy does not change it |
| Grant allocation | `GetEligibleTileKeys` → `allocator.SelectTargetTile` → `state.AddProgress` |
| Current row state | `state.UnlockedRowIndices`, `state.CompletedTiles`, `_completedPointsByRow` |
