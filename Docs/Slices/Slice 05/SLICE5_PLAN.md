# Slice 5: Simulation Execution (Local Execution First) — Plan Only

**Scope:** Implement simulation execution with local (Web-hosted) executor first: Run Simulations UI, batch/run persistence, strategy-driven simulation engine, per-run and batch-level results, Results UI, retry/terminal failure handling, observability knobs, and Slice 4 seeding extension.

**Source of truth:** `Docs/06_Acceptance_Tests.md` — Sections "5) Simulation Execution", "6) Reproducibility & Seeds", "7) Results & Aggregations", "8) Observability & Performance Testability", "9) UI Expectations" (Run Simulations + Results pages).

**Constraints:** Clean Architecture; Web server-rendered; EF Core + Postgres via Infrastructure. Do **not** implement distributed workers/messaging in the first iteration; focus on local execution path. Design so distributed workers can be added later with minimal change.

---

## 1) Domain / Application Design for Simulation Engine Placement

### 1.1 Where the Engine Lives

- **Simulation engine lives in Application** (`BingoSim.Application`), not in Core or Infrastructure.
  - **Core** remains dependency-free: entities, value objects, domain rules (e.g. row unlock rule), and interfaces only. No EF, no RNG, no time sampling.
  - **Application** owns:
    - Snapshot building (Event + Teams + Strategies + resolved Activities/Players → serializable snapshot DTO).
    - Simulation **orchestration**: one “run” = one call that drives simulated time, applies strategy, and produces per-team results.
    - Strategy **execution** (which tile gets a progress grant): strategy implementations live in Application (e.g. `RowRushAllocator`, `GreedyPointsAllocator`) implementing an Application-level interface (e.g. `IProgressAllocator`).
    - RNG usage: Application receives a seed (or derived seed per run) and uses a deterministic RNG (e.g. `Random` with seed, or a small wrapper) for all rolls and time sampling within that run.
  - **Infrastructure** implements repositories for `SimulationBatch`, `SimulationRun`, `EventSnapshot`, run results, and timeline data; it does **not** implement simulation logic.

### 1.2 Core vs Application Boundaries

- **Core** may contain:
  - **Domain rules** expressed as pure functions or small types (e.g. “row N unlocks when sum of completed tile points in row N−1 ≥ UnlockPointsRequiredPerRow”) if we want them testable without Application. Alternatively these can live in Application as part of the run state machine; plan assumes **Application** holds the “run loop” and applies Core value objects (Row, Tile, TileActivityRule, ProgressGrant, etc.) as read-only config.
  - **No simulation runner in Core**; Core has no notion of “current sim time” or “RNG.”
- **Application** contains:
  - **SimulationRunner** (or equivalent): loads snapshot, advances simulated time, processes activity attempts (per-player and per-group), applies strategy to allocate progress to best eligible tile, enforces event duration and row unlock rules, produces **RunResult** (per-team aggregates + timelines).
  - **Snapshot builder**: given EventId + list of TeamIds (or “all drafted teams for event”), loads Event, Teams with StrategyConfigs and TeamPlayers, resolves PlayerProfiles and ActivityDefinitions, serializes to an **EventSnapshot** DTO (JSON) stored by Infrastructure.
  - **Strategy allocators**: `IProgressAllocator` with implementations for RowRush and GreedyPoints; called by the runner when a ProgressGrant occurs to decide which eligible tile(s) receive the units.

### 1.3 Interfaces in Core (if any)

- Core does **not** need an interface for “run simulation.” Persistence interfaces live in Core: `ISimulationBatchRepository`, `ISimulationRunRepository` (and optionally snapshot/result repositories). Application defines and uses these; Infrastructure implements them.

### 1.4 Summary

- **Engine placement:** Application.
- **Core:** Entities (SimulationBatch, SimulationRun, EventSnapshot, result/timeline entities as needed), value objects (unchanged), repository interfaces for batch/run/snapshot/result.
- **Application:** Snapshot builder, SimulationRunner, IProgressAllocator + RowRush + GreedyPoints, RNG usage, batch start command (creates batch + snapshot + run work items), run executor (load run + snapshot → execute → persist results).

---

## 2) Entities / Tables Required

### 2.1 SimulationBatch

- **Location:** `BingoSim.Core/Entities/SimulationBatch.cs`
- **Properties:**
  - `Id` (Guid)
  - `EventId` (Guid) — FK to Event (reference only; config is in snapshot)
  - `Name` (string, optional) or use batch id for display
  - `RunsRequested` (int)
  - `Seed` (long or string) — user-provided or system-generated; stored for reproducibility
  - `ExecutionMode` (enum: Local, Distributed) — for UI and future worker routing
  - `Status` (enum: Pending, Running, Completed, Error)
  - `ErrorMessage` (string, nullable) — high-level message when Status = Error
  - `CreatedAt` (DateTimeOffset)
  - `CompletedAt` (DateTimeOffset?)
- **Relationships:** Has many SimulationRuns.
- **Purpose:** One record per “start batch” request; tracks requested count, seed, mode, and terminal status.

### 2.2 EventSnapshot

- **Location:** `BingoSim.Core/Entities/EventSnapshot.cs` (or value stored under batch)
- **Properties:**
  - `Id` (Guid)
  - `SimulationBatchId` (Guid) — FK to SimulationBatch (1:1: one snapshot per batch)
  - `EventConfigJson` (string) — serialized event (name, duration, unlock points, rows/tiles/rules) + resolved activity definitions + resolved player profiles for each team
  - `CreatedAt` (DateTimeOffset)
- **Purpose:** Effective config at batch start; workers (or local executor) read this only. No reference to live Event/Team after creation.
- **Alternative:** Store `EventConfigJson` directly on SimulationBatch (single JSON column) to avoid extra table; plan recommends **separate EventSnapshot** table for clarity and to match architecture doc.

### 2.3 SimulationRun

- **Location:** `BingoSim.Core/Entities/SimulationRun.cs`
- **Properties:**
  - `Id` (Guid)
  - `SimulationBatchId` (Guid) — FK to SimulationBatch
  - `RunIndex` (int) — 0-based index within batch (for deterministic seed derivation: seed + run index)
  - `Seed` (long or string) — per-run seed (derived from batch seed + RunIndex)
  - `Status` (enum: Pending, Running, Completed, Failed)
  - `AttemptCount` (int) — retry attempts (1..5)
  - `LastError` (string, nullable) — truncated error message on failure
  - `LastAttemptAt` (DateTimeOffset?)
  - `StartedAt` (DateTimeOffset?)
  - `CompletedAt` (DateTimeOffset?)
- **Relationships:** Has many TeamRunResult (or single table with run id + team id).
- **Purpose:** One work item per run; supports retries and terminal failure tracking.

### 2.4 RunResult / TeamRunResult (per-run aggregates)

- **Naming:** Domain doc uses “TeamSimulationResult”; plan uses **TeamRunResult** for per-run, per-team row to avoid confusion with batch-level aggregates.
- **Location:** `BingoSim.Core/Entities/TeamRunResult.cs`
- **Properties:**
  - `Id` (Guid)
  - `SimulationRunId` (Guid) — FK to SimulationRun
  - `TeamId` (Guid) — from snapshot (logical; no FK to live Team)
  - `TeamName` (string)
  - `StrategyKey` (string)
  - `StrategyParamsJson` (string, nullable)
  - `TotalPoints` (int)
  - `TilesCompletedCount` (int)
  - `RowReached` (int) — max row index reached (or similar definition)
  - `IsWinner` (bool)
  - `CreatedAt` (DateTimeOffset)
- **Relationships:** Belongs to SimulationRun.
- **Purpose:** One row per team per run; stored so batch-level aggregates can be computed from these rows (or precomputed and stored on batch).

### 2.5 Timeline Entities (row unlock + tile completion times)

- **Option A — Separate tables:**
  - **RowUnlockTime:** `SimulationRunId`, `TeamId` (logical), `RowIndex`, `UnlockedAtSimTimeSeconds`
  - **TileCompletionTime:** `SimulationRunId`, `TeamId`, `TileKey`, `CompletedAtSimTimeSeconds`
- **Option B — JSON on TeamRunResult:** Two columns on `TeamRunResult`: `RowUnlockTimesJson`, `TileCompletionTimesJson` (e.g. `Dictionary<int,int>` and `Dictionary<string,int>`).
- **Recommendation:** **Option B** for v1 simplicity: store timeline data as JSON on `TeamRunResult`. Reduces tables and matches “per-team, per-run” grain. UI can deserialize for display.

### 2.6 Batch-Level Aggregates

- **Option A — Computed on read:** Query TeamRunResults for the batch, group by TeamId, compute mean/min/max for points, tiles completed, row reached, and winner rate. No extra table.
- **Option B — Persisted BatchAggregates table:** One row per (BatchId, TeamId) with precomputed MeanPoints, MinPoints, MaxPoints, MeanTilesCompleted, MinTilesCompleted, MaxTilesCompleted, MeanRowReached, MinRowReached, MaxRowReached, WinnerRate, etc. Updated when batch completes (or incrementally if desired).
- **Recommendation:** **Option B** for “UI must not recompute” (acceptance test 7). Table: **BatchTeamAggregate** (or **SimulationBatchTeamResult**):
  - `SimulationBatchId`, `TeamId` (logical), `TeamName`, `StrategyKey`
  - `MeanPoints`, `MinPoints`, `MaxPoints`
  - `MeanTilesCompleted`, `MinTilesCompleted`, `MaxTilesCompleted`
  - `MeanRowReached`, `MinRowReached`, `MaxRowReached`
  - `WinnerRate` (e.g. count of wins / total runs for that team in batch)
  - `RunCount` (number of runs included — may be less than batch total if some failed)
- When all runs for a batch are terminal (Completed or Failed), a job or the last run’s completion logic computes and persists/updates BatchTeamAggregate rows. Local executor can do this after processing the last run.

### 2.7 Entity Summary Table

| Entity              | Purpose                                      | Key FKs / Notes                    |
|---------------------|----------------------------------------------|------------------------------------|
| SimulationBatch     | One per “start batch” request                | EventId (reference)                |
| EventSnapshot       | Config snapshot for batch                    | SimulationBatchId (1:1)             |
| SimulationRun       | One work item per run; retry state           | SimulationBatchId, RunIndex, Seed   |
| TeamRunResult       | Per-run, per-team aggregates + timeline JSON| SimulationRunId, TeamId (logical)   |
| BatchTeamAggregate  | Precomputed per-team stats for batch         | SimulationBatchId, TeamId (logical) |

### 2.8 Repository Interfaces (Core)

- `ISimulationBatchRepository`: Add, GetById, Update (status, CompletedAt, ErrorMessage), optional List by EventId or status.
- `IEventSnapshotRepository`: Add, GetByBatchId (or GetByBatchIdAsync).
- `ISimulationRunRepository`: Add, GetById, GetByBatchId (for progress counts), Update (status, AttemptCount, LastError, LastAttemptAt, StartedAt, CompletedAt).
- `ITeamRunResultRepository`: AddRange (per run), GetByRunId, GetByBatchId (for batch view).
- `IBatchTeamAggregateRepository`: AddOrUpdateRange (per batch), GetByBatchId.

---

## 3) Strategy Execution Model (RowRush and GreedyPoints)

### 3.1 Contract (Application)

- **Interface:** `IProgressAllocator` (Application).
  - Input: current run state for one team (unlocked rows, per-tile progress, tile definitions, event rules), plus a **ProgressGrant** (DropKey, Units) and optionally which activity/attempt produced it.
  - Output: which **eligible** tile(s) receive the grant (e.g. one tile key + units, or split). Eligibility: tile must be in an unlocked row, accept that DropKey (via TileActivityRule), and not already completed.
  - Strategy is “per team”; runner calls allocator once per team when a grant is attributed to that team (e.g. per-player roll → one team’s player → one grant to that team; per-group roll → one grant to the team).

### 3.2 RowRush (Baseline)

- **Goal:** Complete rows in order; prioritize tiles that unlock the next row.
- **Rule:** Among eligible tiles (unlocked row, accepts DropKey, not completed), prefer the tile in the **lowest row index** (row 0 first). Within the same row, prefer **lowest points** (1 then 2 then 3 then 4) to maximize “points completed in this row” for unlock progress.
- **Allocation:** Grant full units to a single tile (the one chosen). If multiple tiles tie (same row, same points), pick by deterministic rule (e.g. first by tile key order) so that same seed → same allocation.

### 3.3 GreedyPoints (Alternative)

- **Goal:** Maximize total points completed as fast as possible, regardless of row order.
- **Rule:** Among eligible tiles, prefer the tile with **highest points** (4 then 3 then 2 then 1). Within same points, prefer **lowest row index** (to avoid leaving early rows incomplete if that matters for tie-breaks). Deterministic tie-break (e.g. tile key).
- **Allocation:** Grant full units to one tile.

### 3.4 Minimal but Correct

- Both strategies return a **single** target tile per grant (no splitting in v1). Runner applies full Units to that tile’s progress; when progress ≥ RequiredCount, mark tile completed and record completion time (current sim time). Row unlock is then re-evaluated (domain rule: row N unlocks when points completed in row N−1 ≥ UnlockPointsRequiredPerRow).

### 3.5 ParamsJson

- v1 can ignore ParamsJson for RowRush and GreedyPoints (no parameters required). Strategy implementations read StrategyKey only. ParamsJson stored and displayed in Results; future slices may use it for tuning.

### 3.6 Registration

- Application DI: register `IProgressAllocator` by strategy key (e.g. dictionary or factory that returns RowRushAllocator for RowRush, GreedyPointsAllocator for GreedyPoints). Runner looks up allocator by team’s StrategyKey from snapshot.

---

## 4) Local Execution Orchestration Approach

### 4.1 Fire-and-Forget; UI Does Not Hang

- **Start batch** (Web): User selects Event, sees drafted teams (from Slice 4), enters run count, optional seed, execution mode (Local / Distributed). On submit, Web calls Application command **StartSimulationBatch**.
- **StartSimulationBatch** (Application):
  1. Validate event exists, at least one team exists for event, run count ≥ 1.
  2. Generate or accept seed (store on batch).
  3. Create **SimulationBatch** (Status = Pending or Running), **EventSnapshot** (snapshot JSON built from Event + Teams + resolved Activities/Players).
  4. Create **SimulationRun** rows for 1..RunsRequested (RunIndex 0..N-1, each with derived seed, Status = Pending).
  5. If execution mode is **Local**: enqueue work to a **hosted in-process queue** and return immediately. If **Distributed**: publish message (stubbed for Slice 5; no-op or log).
  6. Return batch Id to UI; UI redirects or links to Results page for that batch.

### 4.2 In-Process Executor (Local)

- **Option A — BackgroundService + channel/queue:** A hosted service (e.g. `SimulationRunQueueHostedService`) that:
  - Reads from a bounded channel (or `Channel<SimulationRunWorkItem>`). “Start batch” pushes RunIds (or run work items) into the channel.
  - Worker loop: dequeue run id, load run + snapshot, execute simulation (Application SimulationRunner), persist TeamRunResult + update run status, optionally update batch progress; repeat. Concurrency: configurable number of concurrent run executions (e.g. 1 for determinism, or 4 for throughput).
- **Option B — Task.Run + semaphore:** On StartSimulationBatch, for Local mode, start a fire-and-forget task (do not await) that loops over run ids and executes them with a semaphore-limited degree of parallelism. Simpler but less “queue-like”; cancellation and shutdown are harder.
- **Recommendation:** **Option A** (BackgroundService + channel). Clear ownership of “who drains the queue”; Web can register the hosted service only when Local execution is enabled. Queue holds run ids (or batch id + run indices); executor pulls from queue, runs simulation, persists results, and checks batch completion to compute BatchTeamAggregate when all runs are done.

### 4.3 Concurrency and Throttle Knobs

- **Local executor:** `LocalSimulationOptions.MaxConcurrentRuns` (e.g. 1–8). Optional: `SimulationDelayMs` (artificial delay per run) for “test mode throttle” to validate that multi-worker scaling would help (observability acceptance).
- These same knobs can be reused later for distributed workers (e.g. worker process reads “max concurrent runs” from config).

### 4.4 Batch Completion

- After each run completes (or fails), executor checks whether all runs for that batch are terminal. If yes: set SimulationBatch.Status = Completed (or Error if any run Failed), set CompletedAt, compute and persist **BatchTeamAggregate** from TeamRunResult rows, optionally set ErrorMessage if Status = Error.

### 4.5 No Distributed Workers in Slice 5

- Distributed path: StartSimulationBatch can create batch + snapshot + runs and “publish” a message that no consumer handles yet (or a stub consumer that does nothing). UI still shows “Distributed” as an option; message contract can exist so that adding a real worker later only requires implementing the consumer and pointing the queue to workers.

---

## 5) UI Pages: Run Simulations + Results View

### 5.1 Run Simulations Page

- **Route:** e.g. `@page "/simulations/run"` or `@page "/run"` (align with existing nav).
- **Content:**
  1. **Select Event:** Dropdown or list of events (from IEventService). On select, load drafted teams for that event (ITeamService.GetByEventIdAsync). Display teams and their strategy configs (StrategyKey, ParamsJson truncated).
  2. **Run count:** Integer input (min 1, max capped e.g. 100_000 or config-driven).
  3. **Seed (optional):** Text or number input. If empty, backend generates and stores a seed.
  4. **Execution mode:** Radio or dropdown: Local execution / Distributed workers (Distributed can show “Coming soon” or be stubbed).
  5. **Start batch:** Button. On submit: call StartSimulationBatch (event id, run count, seed, mode). Return batch id; redirect to Results page for that batch (e.g. `/simulations/results/{batchId}`).
- **Behavior:** Fire-and-forget; no waiting for completion. User sees “Batch started” and is taken to Results page where they can watch progress.

### 5.2 Results Page (View One Batch)

- **Route:** `@page "/simulations/results/{BatchId:guid}"`.
- **Content:**
  1. **Batch summary:** Batch id, event name, runs requested, seed, status (Pending/Running/Completed/Error). If Error, show high-level error message (e.g. “One or more runs failed after 5 attempts”).
  2. **Progress:** Completed count / Failed count / Running count / Pending count (e.g. “45 completed, 2 failed, 3 running, 50 pending”). Use coarse status; avoid recomputing from raw runs on every poll if possible (can store counts on batch or query run statuses).
  3. **Per-team metrics (when batch is completed or partial):** For each team (from snapshot or from BatchTeamAggregate): Winner rate, Mean/Min/Max points, Mean/Min/Max tiles completed, Mean/Min/Max row reached. Source: BatchTeamAggregate table when available; otherwise could fall back to computing from TeamRunResult for that batch.
  4. **Timelines:** Per team: row unlock times (row index → sim time seconds), tile completion times (tile key → sim time seconds). Data from TeamRunResult timeline JSON; show in tables or simple lists. Can be “expand per team” or tabs.
- **Avoid hanging:** Page does not block on batch completion. Use polling (e.g. refresh every 2–5 seconds when status is Running) or optional SignalR later (out of scope for Slice 5). Progress bar or spinner for “Running” state.

### 5.3 Navigation

- Add “Run Simulations” (or “Simulations”) to main nav → Run Simulations page.
- From Run Simulations page, after start → redirect to Results/{batchId}.
- Optional: “Recent batches” list on Run Simulations page linking to Results/{batchId}.

### 5.4 Rerun by Seed (Acceptance 6)

- **Rerun a specific run by seed:** From Results page (or run detail), show “Rerun this run” (or “Rerun with same seed”). Action: Start a new batch with RunsRequested = 1 and Seed = that run’s recorded seed (and same event/teams via snapshot or new snapshot). Results: new batch with one run; identical results expected. Implementation: “Rerun” button that calls StartSimulationBatch(..., runCount: 1, seed: run.Seed) with same event/team selection (or snapshot id). Minimal: store seed on run; UI exposes “Rerun with seed” and pre-fills seed on Run Simulations page.

---

## 6) Retry and Idempotency Rules

### 6.1 Retry (Per Run)

- When a run **fails** (exception during execution): increment **AttemptCount** on SimulationRun, set **LastError** (truncated), **LastAttemptAt** = now. If AttemptCount < 5: re-queue the same run (or leave status Pending so executor picks it up again). If AttemptCount >= 5: set run Status = **Failed**, do not retry; mark batch as **Error** (or leave batch Running and let “batch completion” logic set Error when any run is Failed).
- **Idempotency of execution:** Running the same run twice with the same seed must produce the same result. Runner uses run.Seed for RNG; no side effects from “retry” other than overwriting TeamRunResult for that run (upsert by SimulationRunId + TeamId).

### 6.2 Batch Status

- **Pending:** Batch created, runs created, not yet picked up (e.g. queue empty or not started).
- **Running:** At least one run is Pending or Running.
- **Completed:** All runs are Completed (no Failed).
- **Error:** At least one run is Failed (after 5 attempts). Set ErrorMessage on batch (e.g. “1 run(s) failed after 5 attempts”).

### 6.3 Idempotency of “Start Batch”

- Start batch is not idempotent by design: each submit creates a new SimulationBatch and new runs. No “dedupe by request id” in Slice 5 unless required later.

---

## 7) Metrics Approach

### 7.1 Structured Logging

- All simulation-related log entries include **BatchId** and **RunId** (and optionally TeamId, StrategyKey). Use structured properties (e.g. Serilog `{@BatchId}`) so logs are queryable.

### 7.2 Basic Metrics (Observability Acceptance 8)

- **Expose:** runs completed, runs failed, runs retried (total attempt count minus run count?), estimated runs/sec, batch duration (CompletedAt − CreatedAt for batch).
- **Implementation options:**
  - **In-process:** A small `ISimulationMetrics` (Application) implemented in Infrastructure that increments counters (e.g. runs completed, runs failed) and records batch start/end. Expose via `/metrics` endpoint (e.g. Prometheus) or app-specific “diagnostics” endpoint. Counters can be in-memory for v1.
  - **Or:** Use .NET metrics API (`System.Diagnostics.Metrics`) with counters and histograms; export to Prometheus or EventCounter. Application or executor calls `meter.CreateCounter("simulation.runs.completed")`, etc.
- **Recommendation:** Use **System.Diagnostics.Metrics** (Meter, Counter) in Application (or Infrastructure) for runs completed, runs failed, retries; and a histogram or value for “batch duration” when batch completes. No distributed metrics server required for Slice 5; just “available” so that when we add Prometheus/OpenTelemetry later, we plug in the exporter.

### 7.3 Test Mode Throttle

- **Config:** e.g. `Simulation:DelayMsPerRun` (optional). When set, local executor sleeps that many ms after each run (or before). Allows validating that “2 workers” would improve throughput (two processes = half the effective delay). Document in DEV_SEEDING or config so that performance testability scenario can be run.

---

## 8) Seeding Update Plan (Required for Slice 5)

### 8.1 Extend Dev Seeding to Slice 4

- **Seed Teams** for the two seed events (“Winter Bingo 2025”, “Spring League Bingo”):
  - For each seed event, create 2 teams (e.g. “Team Alpha”, “Team Beta”) with distinct names.
  - Assign players from seed players (e.g. 4 players per team, non-overlapping or overlapping as desired for testing).
- **Seed StrategyConfig** for each seed team:
  - StrategyKey: RowRush for one team, GreedyPoints for the other (or both RowRush for baseline).
  - ParamsJson: sample JSON e.g. `{}` or `{"preferRow": 0}` (minimal; not used by engine in v1).

### 8.2 Stable Keys for Reset

- **Teams:** Identify by (EventId + Team name) or by a stable seed team name list (e.g. “Team Alpha”, “Team Beta”) and event name. Reset: delete teams for seed events by name or by event id.
- **StrategyConfigs:** Deleted when Teams are deleted (cascade or explicit). No separate stable key needed if we delete by team.

### 8.3 Order of Operations

- **SeedAsync:** After SeedEventsAsync, call **SeedTeamsAsync** (and **SeedStrategiesAsync** if separate). SeedTeamsAsync: for each seed event, get event id by name, create 2 teams with TeamPlayers (resolve player ids by SeedPlayerNames), then create StrategyConfig per team.
- **ResetAndSeedAsync:** Delete in order: **StrategyConfigs** (for seed teams) → **Teams** (for seed events) → **Events** → **Activities** → **Players**. StrategyConfigs must be deleted before Teams (FK); Teams before Events (FK). So: StrategyConfigs → TeamPlayers → Teams → Events → Activities → Players. (TeamPlayers before Teams if FK requires it; typically cascade delete Team + StrategyConfig when deleting Team, so order can be: Teams for seed events → Events → Activities → Players. If Team has FK to Event, delete Teams first then Events.)

  - Concrete order: For each seed event name, get event; get teams by event id; for each team delete StrategyConfig and TeamPlayers then Team (or use repository DeleteAsync which cascades). Then delete Events, Activities, Players as today.

### 8.4 DEV_SEEDING.md Updates

- State that dev seeding now includes **Slice 4**: Teams and StrategyConfigs for seed events.
- Document reset order: StrategyConfigs → Teams → Events → Activities → Players (or “Teams for seed events, then Events, then Activities, then Players” with cascade from Team to StrategyConfig and TeamPlayers).
- List what’s seeded: 2 teams per seed event, with RowRush/GreedyPoints and sample ParamsJson.

### 8.5 Files to Touch

- **BingoSim.Application:** DevSeedService — add SeedTeamNames / SeedTeamKeys, SeedTeamsAsync, call from SeedAsync; in ResetAndSeedAsync, delete seed teams (and their strategy configs + team players) before deleting events. Ensure ITeamRepository is used (GetByEventIdAsync, AddAsync, DeleteAsync or DeleteAllByEventIdAsync).
- **BingoSim.Seed:** No project change except that seeding now includes teams (via DevSeedService).
- **Docs/DEV_SEEDING.md:** Update “Slices 1–3” to “Slices 1–4”, add section for Slice 4 (Teams + StrategyConfig), update reset order and verify steps.

---

## 9) Test Plan (Unit + Integration, Including Reproducibility)

### 9.1 Unit Tests (Application)

- **Snapshot builder:** Given Event + Teams (with strategies and players), build snapshot JSON; assert it contains event config, team names, strategy keys, and resolved activity/player data. No DB; use in-memory entities.
- **RowRush allocator:** Given unlocked rows, a set of eligible tiles (same row, same points), assert the chosen tile is the one with lowest row index and lowest points; tie-break by key.
- **GreedyPoints allocator:** Given eligible tiles, assert the chosen tile has highest points, then lowest row index; tie-break by key.
- **Seed derivation:** Given batch seed and run index, assert derived run seed is deterministic (e.g. hash or formula).
- **SimulationRunner (isolated):** Mock or minimal snapshot (one row, one tile, one activity, one outcome with one grant). Run with fixed seed; assert one run produces expected TeamRunResult (points, tile completed, row reached) and timeline entries. Second run with same seed produces identical result (reproducibility).

### 9.2 Unit Tests (Core)

- **SimulationBatch / SimulationRun:** Status transitions, AttemptCount boundaries. Optional: domain rules if any live in Core.

### 9.3 Integration Tests (Infrastructure)

- **SimulationBatchRepository:** Create batch, get by id, update status.
- **SimulationRunRepository:** Create runs for a batch, get by batch id, update status and attempt count.
- **TeamRunResultRepository:** Add results for a run, get by run id, get by batch id.
- **BatchTeamAggregateRepository:** Insert/update aggregates for a batch, get by batch id.
- **EventSnapshotRepository:** Save and load snapshot by batch id.
- **End-to-end persistence:** Create batch + snapshot + runs; executor (or test) “completes” two runs with result rows; compute and save BatchTeamAggregate; query batch and assert aggregates and run counts.

### 9.4 Reproducibility and Rerun-by-Seed

- **Test: Same seed → same result.** Run SimulationRunner twice with same snapshot and same run seed; assert both outputs (TeamRunResult + timelines) are byte-for-byte or value-equal.
- **Test: Rerun by seed (application level).** Start batch with run count 1 and seed S; get run result. Start another batch with run count 1 and seed S (same event/teams); get run result. Assert key outputs (points, tiles completed, row reached, winner) match.
- **Test: Different run index, same batch seed.** Run A with (batchSeed, index 0) and run B with (batchSeed, index 1); assert derived seeds differ and results can differ.

### 9.5 Worker / Executor Tests (Application or Worker)

- **Local executor:** Unit test with mock queue: enqueue two run ids; executor processes both and persists results; assert run statuses updated and BatchTeamAggregate written when batch complete. Optional: test retry (simulate failure, assert AttemptCount increments and eventually Failed after 5).

### 9.6 Web Tests (Optional for Slice 5)

- **Run Simulations page:** Renders event dropdown, run count, seed, mode; submit calls StartSimulationBatch and redirects to results URL (bUnit).
- **Results page:** Renders batch summary and progress; when batch completed, shows per-team metrics (mock or test data).

### 9.7 Test Summary Table

| Area           | Tests                                                                 |
|----------------|-----------------------------------------------------------------------|
| Snapshot       | Build snapshot from Event + Teams; contains expected config           |
| RowRush        | Eligible tiles → correct tile chosen (row/points/tie-break)          |
| GreedyPoints   | Eligible tiles → correct tile chosen (points/row/tie-break)           |
| Seed derivation| Batch seed + index → deterministic run seed                           |
| Runner         | Minimal snapshot, fixed seed → expected result; same seed → same result |
| Reproducibility| Two batches, same seed, same config → identical run results           |
| Repositories   | CRUD for Batch, Run, Snapshot, TeamRunResult, BatchTeamAggregate      |
| Executor       | Queue processing, status updates, batch completion, aggregate write   |
| Retry          | Fail 5 times → run Failed, batch Error                                |

---

## 10) Exact List of Files to Create or Modify (Summary)

### Create (by layer)

- **Core:** SimulationBatch.cs, SimulationRun.cs, EventSnapshot.cs, TeamRunResult.cs, BatchTeamAggregate.cs (or equivalent name), RunStatus enum, BatchStatus enum; Exceptions (e.g. SimulationBatchNotFoundException); Interfaces: ISimulationBatchRepository, IEventSnapshotRepository, ISimulationRunRepository, ITeamRunResultRepository, IBatchTeamAggregateRepository.
- **Application:** DTOs (StartSimulationBatchRequest, SimulationBatchResponse, BatchProgressResponse, TeamRunResultResponse, BatchTeamAggregateResponse, etc.); EventSnapshotDto + snapshot builder; SimulationRunner; IProgressAllocator, RowRushAllocator, GreedyPointsAllocator; StartSimulationBatchCommand/Handler or StartSimulationBatchService; SimulationRunExecutor (or similar) used by hosted service; optional ISimulationMetrics; registration of allocators by key.
- **Infrastructure:** Configurations for new entities; migrations; repositories for batch, snapshot, run, team run result, batch aggregate; optional metrics implementation.
- **Web:** RunSimulations.razor (run page), SimulationResults.razor (results page); nav link; optional polling for results.
- **Tests:** Application unit tests (snapshot, allocators, runner, reproducibility); Core entity tests; Infrastructure integration tests (repositories, e2e persistence); optional Web bUnit tests.

### Modify

- **Infrastructure:** AppDbContext (new DbSets); DependencyInjection (new repositories, optional metrics).
- **Web:** Program.cs or host builder (register LocalSimulationOptions, channel, SimulationRunQueueHostedService); nav.
- **Application:** DevSeedService (SeedTeamsAsync, SeedStrategiesAsync or inline in SeedTeamsAsync; reset order); DependencyInjection for new services and allocators.
- **Docs:** DEV_SEEDING.md (Slice 4 seeding, reset order).

---

## Summary

- **Engine in Application:** Snapshot builder, SimulationRunner, RowRush/GreedyPoints allocators, RNG with seed; Core has entities and repository interfaces only.
- **Entities:** SimulationBatch, EventSnapshot, SimulationRun, TeamRunResult (with timeline JSON), BatchTeamAggregate.
- **Local execution:** BackgroundService + channel; StartSimulationBatch creates batch + snapshot + runs and enqueues run ids; executor drains queue with configurable concurrency and optional delay.
- **UI:** Run Simulations (event, teams, run count, seed, mode, start) and Results (progress, per-team metrics, timelines); fire-and-forget, no hang.
- **Retry:** Up to 5 attempts per run; then Failed; batch marked Error.
- **Metrics:** Structured logging (BatchId, RunId); counters for runs completed/failed/retried and batch duration via System.Diagnostics.Metrics.
- **Seeding:** Extend to Slice 4 (Teams + StrategyConfig for seed events); reset order StrategyConfigs → Teams → Events → Activities → Players; update DEV_SEEDING.md.
- **Tests:** Unit (allocators, snapshot, runner, reproducibility), integration (repositories, e2e), optional Web and executor tests.

No code is written in this plan; implementation follows in a subsequent step.
