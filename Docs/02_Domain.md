# Domain Model

This document defines the core domain entities (nouns), their key fields, and relationships. It intentionally avoids implementation details and algorithms.

## Conventions

### Base Entity
All persisted entities derive from a shared base shape unless otherwise stated.

- Id: Guid
- CreatedAt: DateTimeOffset

### Time
- All schedules are interpreted in Eastern Time (America/New_York).
- Simulation time is modeled in coarse units (seconds/minutes), not game ticks.

---

## Core Reference Concepts (Reusable Libraries)

These entities are intended to be reusable across events and simulation runs.

### Capability
Represents a player attribute that may:
- gate eligibility for an Activity/Tile (requirement), and/or
- provide optional modifiers that make an Activity faster/easier

> Note: Capabilities are player-owned. Activities define how a Capability is used (required vs modifier).

Fields:
- Key: string (stable identifier, e.g. "quest.ds2", "item.dragon_hunter_lance")
- Name: string (display name)

Relationships:
- PlayerProfile has many Capabilities

---

### PlayerProfile
Represents a reusable player definition that can be selected into an Event Team.

Fields:
- Name: string
- SkillTimeMultiplier: decimal (e.g. 0.8 faster, 1.2 slower)
- Capabilities: List<Capability>
- WeeklySchedule: WeeklySchedule

Notes:
- PlayerProfile is reused across events; teams are drafted per-event.

---

### WeeklySchedule
Represents a weekly play schedule template (not tied to calendar dates).

Fields:
- Sessions: List<ScheduledSession>

---

### ScheduledSession
Represents a single play session window template.

Fields:
- DayOfWeek: enum (Mon..Sun)
- StartLocalTime: TimeOnly
- DurationMinutes: int

Notes:
- A player may have multiple sessions per day.

---

### ActivityDefinition
Represents an in-game activity (boss, raid, skilling method, etc.) that may be referenced by multiple tiles.

Fields:
- Key: string (stable identifier, e.g. "activity.zulrah")
- Name: string
- ModeSupport: ActivityModeSupport
- Attempts: List<ActivityAttemptDefinition>

Notes:
- Activities are globally reusable across events.

---

### ActivityModeSupport
Declares which group sizes are supported.

Fields:
- SupportsSolo: bool
- SupportsGroup: bool
- MinGroupSize: int? (nullable if supports group but uses defaults)
- MaxGroupSize: int? (nullable)

---

### ActivityAttemptDefinition
Represents one “roll channel” / loot line / attempt definition for an Activity.

Examples:
- per-player loot roll line (rolled once per player)
- a second per-player loot roll line (rolled twice per player)
- team-level rare roll (rolled once per group/team attempt)

Fields:
- Key: string (stable within the activity, e.g. "personal_loot_line_1")
- RollScope: RollScope (PerPlayer | PerGroup)
- TimeModel: AttemptTimeModel
- Outcomes: List<ActivityOutcomeDefinition>

Notes:
- An Activity can have multiple attempt definitions to represent multiple loot lines.

---

### AttemptTimeModel
Defines how attempt time is sampled.

Fields:
- BaselineTimeSeconds: int
- Distribution: TimeDistribution (e.g. Uniform, NormalApprox, Custom)
- VarianceSeconds: int? (optional; interpretation depends on distribution)

Notes:
- Player skill and applicable capability-based modifiers may affect the sampled time during simulation.

---

### ActivityOutcomeDefinition
Represents one possible outcome of a roll with a probability weight.

Fields:
- Key: string
- WeightNumerator: int
- WeightDenominator: int
- Grants: List<ProgressGrant>

Notes:
- Outcomes can grant multiple items/keys (multiple “drops”) in a single outcome.

---

### ProgressGrant
Represents a grant of progress units for a particular DropKey.

Fields:
- DropKey: string (stable identifier, e.g. "drop.magic_fang")
- Units: int (e.g. +1, +3)

---

### GroupScalingRule
Defines how time and probability scale by group size for an Activity.

Fields:
- AppliesToActivity: ActivityDefinition
- SizeBands: List<GroupSizeBand>

---

### GroupSizeBand
Defines scaling for a range of group sizes.

Fields:
- MinSize: int
- MaxSize: int
- TimeMultiplier: decimal (applied to attempt time)
- ProbabilityMultiplier: decimal (applied to outcome weights or derived probabilities)

Notes:
- Bands may cover ranges (e.g. 2–4 share the same scaling).

---

## Event Configuration

These entities define the structure of a single event instance.

### Event
Represents one configured community event and its board.

Fields:
- Name: string
- Duration: TimeSpan
- UnlockPointsRequiredPerRow: int (global default is 5; event stores effective value)
- Rows: List<Row>

Relationships:
- Event has many Teams
- Event has ordered Rows

---

### Row
Represents an ordered row/pack of tiles.

Fields:
- Index: int (0-based or 1-based; must be consistent)
- Tiles: List<Tile>

Constraints:
- Each Row contains four tiles with Points {1,2,3,4} (as a domain rule).

---

### Tile
Represents an event-specific goal worth points.

Fields:
- Key: string (stable within the event/board)
- Name: string
- Points: int (1,2,3,4)
- RowIndex: int
- RequiredCount: int (completion threshold in progress units)
- AllowedActivities: List<TileActivityRule>

Notes:
- A Tile may be satisfied by one or more Activities.
- A Tile’s interpretation of drops/progress is defined via its TileActivityRules.

---

### TileActivityRule
Defines how a particular Activity contributes progress to a Tile.

Purpose:
- Allows the same Activity to contribute differently to different Tiles (even with the same DropKey).

Fields:
- Activity: ActivityDefinition
- AcceptedDropKeys: List<string>
- DropKeyWeights: Dictionary<string, int>? (optional: if multiple keys can progress the tile, weights guide “best eligible tile” allocation)
- Requirements: List<Capability> (must-have to allow this activity for this tile)
- Modifiers: List<ActivityModifierRule> (optional capability-based speed/prob adjustments)

Notes:
- Requirements are eligibility gates.
- Modifiers are optional and may improve attempt time/probability if present.

---

### ActivityModifierRule
Defines an optional capability-based modifier.

Fields:
- Capability: Capability
- TimeMultiplier: decimal? (optional)
- ProbabilityMultiplier: decimal? (optional)

Notes:
- A modifier applies only if the player (or group members, depending on design) possesses the capability.
- Eligibility is unaffected unless the capability is also listed as a Requirement.

---

### Team
Represents a drafted team for a specific event instance.

Fields:
- Name: string
- EventId: Guid

Relationships:
- Team has many TeamPlayers
- Team has one StrategyConfig (for simulation)

Notes:
- Teams are per-event and are not reused across events.

---

### TeamPlayer
Membership entity linking a reusable PlayerProfile into a specific Team.

Fields:
- TeamId: Guid
- PlayerProfileId: Guid

Notes:
- Enables reuse of PlayerProfiles across events without embedding Team on PlayerProfile.

---

### StrategyConfig
Represents a strategy selection with optional parameters for a team.

Fields:
- StrategyKey: string (e.g. "GreedyPoints", "RowRush", "Balanced")
- ParametersJson: string (user-provided JSON blob)

Relationships:
- Team has exactly one StrategyConfig for a given simulation run (or default assigned in UI)

---

## Per-Team Event State (during a simulation)

These entities represent evolving state for a Team during an Event simulation.

### TeamRowUnlockState
Tracks when each row became unlocked for a team.

Fields:
- TeamId: Guid
- RowIndex: int
- UnlockedAtSimTimeSeconds: int

Constraints:
- Row 1 (or index 0) is unlocked at sim time 0.
- Unlock is monotonic; rows never relock.

---

### TeamTileProgress
Tracks progress units for a tile for a specific team.

Fields:
- TeamId: Guid
- TileKey: string
- ProgressUnits: int
- CompletedAtSimTimeSeconds: int? (null if not completed)

Constraints:
- Tile completion occurs when ProgressUnits >= Tile.RequiredCount.
- Completed tiles cannot be repeated.

---

## Simulation Execution & Results (Persisted, v1 aggregated)

### SimulationBatch
Represents a user-initiated batch of many simulation runs for comparison.

Fields:
- Name: string
- RunsRequested: int
- CreatedBy: string? (optional; likely constant for single-user)
- Notes: string? (optional)

Relationships:
- SimulationBatch has many SimulationRuns

---

### SimulationRun
Represents one simulation execution instance.

Fields:
- BatchId: Guid
- EventSnapshotId: Guid (or serialized snapshot reference)
- StartedAt: DateTimeOffset
- CompletedAt: DateTimeOffset?

Relationships:
- SimulationRun has many TeamSimulationResult

Notes:
- v1 persistence stores aggregated results only (no per-attempt trace).

---

### EventSnapshot
Captures the effective configuration used for a simulation run.

Fields:
- EventConfigJson: string

Notes:
- Stored as JSON to keep v1 simple and enable reproducibility without complex relational cloning.

---

### TeamSimulationResult
Aggregated results per team for a SimulationRun.

Fields:
- SimulationRunId: Guid
- TeamName: string
- StrategyKey: string
- StrategyParamsJson: string

Aggregates:
- TotalPoints: int
- TilesCompletedCount: int
- RowReached: int (max unlocked row index or count reached; must define consistently)
- IsWinner: bool

Timing:
- RowUnlockTimes: Dictionary<RowIndex, UnlockedAtSimTimeSeconds>
- TileCompletionTimes: Dictionary<TileKey, CompletedAtSimTimeSeconds>

Notes:
- TileCompletionTimes is optional but desired (explicitly requested).
- RowUnlockTimes and TileCompletionTimes are stored for analysis and UI.

---

## Key Domain Rules Summary

- Event duration is fixed and defined at start.
- Row unlock requires completing at least UnlockPointsRequiredPerRow points from the immediately preceding row.
- Unlock progression is monotonic; no regression.
- Tile completion is monotonic; once complete, it cannot be repeated.
- Strategies are pluggable by team; strategy decides how progress is allocated to the best eligible tile.
- Activities are reusable definitions; tiles define how activities contribute progress via TileActivityRules.
- Activities may contain multiple loot/roll channels (per-player and/or per-group).
- Group scaling is defined via size band lookup tables.

