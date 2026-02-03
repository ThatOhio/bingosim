# BingoSim Feature Audit (February 2025)

**Audit Date:** February 2, 2025  
**Scope:** Independent review of the codebase against specification documents 00–06  
**Method:** Direct code inspection; no reliance on prior gap reviews or Slice documents

---

## 1. Executive Summary

The BingoSim application has achieved substantial implementation coverage across its core specification. The **walking skeleton** (end-to-end flow from libraries → event → teams → batch → results) is fully functional. **Local** and **distributed** execution modes work. Batch aggregates, timelines, seeds, retries, and two strategies (RowRush, GreedyPoints) are implemented.

**Key gaps** center on simulation modeling rather than UI or infrastructure:

1. **Group play & PerGroup roll scope** — Not simulated; all rolls treated as per-player
2. **GroupScalingBands** — Stored and configurable but not applied during simulation
3. **WeeklySchedule / play sessions** — Player schedule is persisted but never used; simulation runs players continuously
4. **ActivityModifierRule** — Capability-based time/probability modifiers exist in config but are not applied in simulation
5. **Failure modeling** — No explicit failure outcomes with time penalties
6. **Rerun specific run** — Only whole-batch rerun exists; no per-run rerun
7. **Worker concurrency** — No configurable in-worker parallelism for distributed mode
8. **Optional metadata** — SimulationBatch lacks Notes and CreatedBy
9. **Dry Run mode** — No explicit single-run / dry-run toggle (user can set run count to 1)
10. **Metrics exposure** — Metrics exist but no dedicated endpoint/page for inspection

---

## 2. Specification Documents Audited

| Document | Purpose |
|----------|---------|
| 00_Vision.md | Product vision, success criteria, non-goals |
| 01_Scope.md | Core concepts, tile model, time model, execution, persistence |
| 02_Domain.md | Domain entities, value objects, relationships |
| 03_User_Flows.md | UI flows for libraries, events, teams, simulations, results |
| 04_Architecture.md | Layering, runtime topology, messaging, failure handling |
| 05_Nonfunctional.md | Performance, retries, idempotency, observability |
| 06_Acceptance_Tests.md | Verifiable acceptance criteria |

---

## 3. Implemented Features (By Document)

### 3.1 Vision (00_Vision.md)

| Feature | Status | Notes |
|---------|--------|-------|
| Web UI configures and launches community event simulation | ✅ | Run Simulations page, event selection, teams, run count, execution mode |
| Worker processes execute simulations to completion | ✅ | Local (in-process) and distributed (RabbitMQ + Worker) |
| Event simulated start-to-finish without manual intervention | ✅ | Batch orchestration, run creation, execution, finalization |
| Results inspected and compared across strategies | ✅ | Per-team aggregates, winner rate, mean/min/max, timelines |
| Single-user, no multi-tenant | ✅ | No auth; internal tool |

---

### 3.2 Scope (01_Scope.md)

| Feature | Status | Notes |
|---------|--------|-------|
| **Players** — Fixed skill, capabilities | ✅ | PlayerProfile: SkillTimeMultiplier, Capabilities |
| **Players** — Realistic play-session behavior | ❌ | WeeklySchedule stored but not used in simulation |
| **Event Teams** — Groups with shared strategy | ✅ | Team, StrategyConfig, TeamPlayer |
| **Events** — Fixed duration, rules at start | ✅ | Event.Duration, UnlockPointsRequiredPerRow |
| **Event Board** — Ordered rows, 4 tiles (1–4 pts) per row | ✅ | Row.Index, Tile.Points |
| **Tile Unlock** — ≥5 pts from previous row | ✅ | RowUnlockHelper, monotonic unlocks |
| **Probabilistic time-based tiles** | ✅ | AttemptTimeModel, outcomes with weights |
| **Eligibility & Requirements** | ✅ | TileActivityRule.Requirements; GetFirstEligibleActivity checks capabilities |
| **Skill effects on time** | ✅ | SkillTimeMultiplier applied in SampleAttemptDuration |
| **Group Tiles** | ❌ | No group formation; no PerGroup rolls; GroupScalingBands unused |
| **Failure modeling** | ❌ | No failure outcomes with time penalties |
| **Strategy & decision making** | ✅ | RowRush, GreedyPoints; allocator selects target tile |
| **Execution model** — Workers, thousands to millions | ✅ | Local + distributed; configurable concurrency (local) |
| **Persistence** — Event config, results, aggregates | ✅ | Postgres; EventSnapshot JSON; TeamRunResult; BatchTeamAggregate |

---

### 3.3 Domain (02_Domain.md)

| Entity/Concept | Status | Notes |
|----------------|--------|-------|
| Capability | ✅ | Value object; PlayerProfile has many |
| PlayerProfile | ✅ | Name, SkillTimeMultiplier, Capabilities, WeeklySchedule |
| WeeklySchedule, ScheduledSession | ⚠️ | Stored, editable; not used in simulation |
| ActivityDefinition | ✅ | Key, Name, ModeSupport, Attempts, GroupScalingBands |
| ActivityModeSupport | ✅ | SupportsSolo, SupportsGroup, Min/MaxGroupSize |
| ActivityAttemptDefinition | ✅ | Key, RollScope, TimeModel, Outcomes |
| AttemptTimeModel | ✅ | BaselineTimeSeconds, Distribution, VarianceSeconds |
| ActivityOutcomeDefinition, ProgressGrant | ✅ | WeightNumerator/Denominator; Grants with DropKey, Units |
| GroupScalingRule / GroupSizeBand | ⚠️ | Stored on ActivityDefinition; not applied in simulation |
| Event, Row, Tile | ✅ | Duration, Rows, Tiles with Points 1–4 |
| TileActivityRule | ✅ | Activity, AcceptedDropKeys, Requirements, Modifiers |
| ActivityModifierRule | ⚠️ | Stored; not included in snapshot; not applied in simulation |
| Team, TeamPlayer, StrategyConfig | ✅ | Per-event teams; StrategyKey, ParamsJson |
| TeamRowUnlockState, TeamTileProgress | ✅ | Modeled in TeamRunState during run |
| SimulationBatch, SimulationRun, EventSnapshot | ✅ | Batch, runs, snapshot JSON |
| TeamSimulationResult | ✅ | TeamRunResult: TotalPoints, TilesCompletedCount, RowReached, IsWinner, timelines |
| BatchTeamAggregate | ✅ | Mean/min/max points, tiles, row; WinnerRate |

**Domain gaps:**

- `TileActivityRuleSnapshotDto` does not include `Modifiers` (ActivityModifierRule); snapshot builder omits them
- `DropKeyWeights` on TileActivityRule (for allocation preference) — not in snapshot or allocator logic
- SimulationBatch: no `Notes`, `CreatedBy` (spec allows optional)

---

### 3.4 User Flows (03_User_Flows.md)

| Flow | Status | Notes |
|------|--------|-------|
| **Flow 1:** Create/Update Player Library | ✅ | Players CRUD; name, skill, capabilities, weekly schedule |
| **Flow 2:** Create/Update Activity Library | ✅ | Activities CRUD; attempts, outcomes, group scaling bands |
| **Flow 3:** Create Event and Board | ✅ | Events CRUD; rows, tiles, TileActivityRules |
| **Flow 4:** Assign Teams and Strategies | ✅ | Event teams page; create team, assign players, StrategyKey + ParamsJson |
| **Flow 5:** Run Simulations (Batch) | ✅ | Select event, teams, run count, seed, execution mode (Local/Distributed) |
| **Flow 5:** Dry Run / Single-Run mode | ⚠️ | No explicit toggle; user can set run count = 1 |
| **Flow 6:** View Simulation Results | ✅ | Batches list; per-batch results; aggregates; timelines |
| Delete confirmation (Players, Activities, Events, Teams) | ✅ | DeleteConfirmationModal used |
| Manual refresh for libraries | ⚠️ | Standard Blazor navigation; no explicit "refresh" button (acceptable for v1) |
| No dashboard (v1) | ✅ | Home is minimal; nav to Players, Activities, Events, Run, Results |

**Primary Navigation:** Players, Activities, Events, Run Simulations, Simulation Results — all present and linked.

---

### 3.5 Architecture (04_Architecture.md)

| Component | Status | Notes |
|-----------|--------|-------|
| BingoSim.Core — domain, no infra | ✅ | Entities, value objects, interfaces |
| BingoSim.Application — use cases | ✅ | Services, DTOs, simulation runner, allocators |
| BingoSim.Infrastructure — adapters | ✅ | EF Core, repos, MassTransit, BatchFinalization |
| BingoSim.Web — presentation | ✅ | Blazor Server; Run Simulations, Results |
| BingoSim.Worker — execution | ✅ | MassTransit consumer; ExecuteSimulationRun |
| Docker topology — Postgres, RabbitMQ, Web, Worker | ✅ | compose.yaml |
| Snapshot-at-start for reproducibility | ✅ | EventSnapshotBuilder; JSON stored with batch |
| Fire-and-forget messaging | ✅ | Web publishes; workers consume; UI polls DB |
| Optional local execution | ✅ | SimulationRunQueueHostedService; LocalSimulationOptions |
| Parallel workers, no ordering | ✅ | MassTransit; TryClaimAsync for idempotency |
| SimulationBatchStarted, ExecuteSimulationRun | ✅ | Shared.Messages contracts |
| Failure metadata (AttemptCount, LastError, Status) | ✅ | SimulationRun |
| Batch completion when all runs terminal | ✅ | BatchFinalizationService; TryTransitionToFinalAsync |

---

### 3.6 Nonfunctional (05_Nonfunctional.md)

| Requirement | Status | Notes |
|-------------|--------|-------|
| Retry up to 5 attempts | ✅ | SimulationRun.MarkFailed; AttemptCount >= 5 → Failed |
| Batch surfaced as error on terminal failures | ✅ | BatchStatus.Error; ErrorMessage |
| At-least-once; DB source of truth | ✅ | TryClaimAsync; idempotent execution |
| Workers independent of Web UI | ✅ | Distributed mode; Web can close |
| Batch-level aggregates stored | ✅ | BatchTeamAggregate; winner rate, mean/min/max |
| Structured logging (BatchId, RunId) | ✅ | Logger calls in executor, consumer |
| Dry-run verbose logging | ⚠️ | Not explicitly toggled by run count |
| Metrics: runs completed/failed/retried, batch duration, throughput | ⚠️ | InMemorySimulationMetrics; used for BatchProgressResponse; no dedicated endpoint |
| Runs currently running (gauge) | ✅ | Derived in GetProgressAsync (Running count) |
| RNG seed recorded per run | ✅ | SimulationRun.Seed; stored in batch |
| Snapshot reproducibility | ✅ | EventSnapshot JSON at batch start |
| No auth (internal-only) | ✅ | No auth middleware |
| Linux x86_64, Docker Compose | ✅ | Dockerfiles; compose.yaml |

**Metrics gap:** 05_Nonfunctional requires "runs completed (counter), runs failed (counter), runs retried (counter), runs currently running (gauge), batch duration (timer), throughput estimate (runs/sec)". These are computed in `GetProgressAsync` for the UI, but there is no dedicated metrics endpoint or page for programmatic inspection. InMemorySimulationMetrics exists but is scoped to batch; no system-wide metrics exposure.

---

### 3.7 Acceptance Tests (06_Acceptance_Tests.md)

| Scenario | Status | Notes |
|----------|--------|-------|
| **0) Walking Skeleton** — Create libs, event, run batch, view results | ✅ | Full flow supported |
| **1) Player Library** — Create, Edit, Delete with confirmation | ✅ | CRUD + DeleteConfirmationModal |
| **2) Activity Library** — Multiple loot lines, PerGroup rare roll, Group scaling bands | ⚠️ | CRUD + bands stored; PerGroup/bands not used in sim |
| **3) Event & Board** — Rows, tiles 1–4, TileActivityRules; multi-activity tiles | ✅ | Event CRUD; multi-activity tiles work |
| **4) Team Drafting & Strategy** | ✅ | Teams, players, StrategyKey, ParamsJson |
| **5) Local execution** — 100 runs | ✅ | Local mode; SimulationRunQueueHostedService |
| **5) Distributed execution** — 1000 runs, Web can close | ✅ | MassTransit; Worker consumer |
| **5) Unlock rules enforced** | ✅ | RowUnlockHelper; row 0 at 0; ≥5 pts for next |
| **5) Strategy controls allocation** | ✅ | Allocator selects target; eligibility enforced |
| **5) Retries and terminal failure tracked** | ✅ | AttemptCount; Failed; batch Error; UI shows |
| **6) Seed in UI** | ✅ | Run Simulations: optional seed input |
| **6) Repeatability with same seed** | ✅ | SeedDerivation; deterministic RNG |
| **6) Rerun specific run by seed** | ❌ | Only "Rerun with same seed" (whole batch); no per-run rerun |
| **7) Batch-level aggregates stored and displayed** | ✅ | BatchTeamAggregate; UI shows mean/min/max |
| **7) Timelines for analysis** | ✅ | RowUnlockTimesJson, TileCompletionTimesJson; sample in UI |
| **8) Metrics for throughput diagnosis** | ⚠️ | RetryCount, Elapsed, Runs/sec in UI; no dedicated metrics page |
| **8) Multi-worker parallelism validation** | ⚠️ | LocalSimulationOptions.SimulationDelayMs for throttle; Worker has no MaxConcurrentRuns; scaling validated by running multiple workers (containers) |
| **9) All pages CRUD** | ✅ | Players, Activities, Events with create/edit/delete; Run Simulations; Results |

---

## 4. Feature Gaps (Summary)

### 4.1 High Priority — Simulation Modeling

| Gap | Spec Reference | Current State |
|-----|----------------|---------------|
| **PerGroup roll scope** | 01_Scope § Group Tiles; 02_Domain § RollScope | SimulationRunner uses only first attempt per activity; all rolls are per-player. PerGroup outcomes (e.g. team rare roll) never simulated. |
| **GroupScalingBands in simulation** | 01_Scope § Group Tiles; 02_Domain § GroupSizeBand | Bands stored and in snapshot; `SampleAttemptDuration` uses only skill multiplier. No group size lookup or time/probability scaling. |
| **Group formation and group tiles** | 01_Scope § Group Tiles | No logic for forming groups, scheduling group attempts, or applying group-specific scaling. All work is per-player. |
| **WeeklySchedule / play sessions** | 01_Scope § Players; 02_Domain § WeeklySchedule | PlayerProfile.WeeklySchedule persisted and editable. Simulation runs all players continuously for full event duration; no session windows. |
| **ActivityModifierRule in simulation** | 02_Domain § ActivityModifierRule | Modifiers (TimeMultiplier, ProbabilityMultiplier) not in TileActivityRuleSnapshotDto; SimulationRunner does not apply them. |

### 4.2 Medium Priority — UX & Reproducibility

| Gap | Spec Reference | Current State |
|-----|----------------|---------------|
| **Rerun specific run** | 06_Acceptance § 6 | "Rerun with same seed" reruns entire batch. No run-details view or per-run rerun action. |
| **Dry Run / Single-Run toggle** | 03_User_Flows § Flow 5 | User can set run count = 1; no explicit "Dry Run" or "Debug single run" mode with extra logging. |
| **SimulationBatch Notes, CreatedBy** | 02_Domain § SimulationBatch | Entity has Name, RunsRequested, Seed, ExecutionMode; no Notes or CreatedBy. |

### 4.3 Lower Priority — Observability & Config

| Gap | Spec Reference | Current State |
|-----|----------------|---------------|
| **Metrics endpoint/page** | 05_Nonfunctional § Metrics | InMemorySimulationMetrics + BatchProgressResponse provide data; no dedicated /metrics or diagnostics page. |
| **Worker MaxConcurrentRuns** | 04_Architecture § Concurrency | Local executor has MaxConcurrentRuns; Worker consumes one message at a time (MassTransit default). No WorkerSimulationOptions.MaxConcurrentRuns. |
| **Failure modeling (tile attempts)** | 01_Scope § Failure Modeling | No explicit failure outcomes; no time penalties (e.g. death recovery). Outcomes can grant 0 units but no penalty duration. |
| **DropKeyWeights in allocation** | 02_Domain § TileActivityRule | DropKeyWeights not in snapshot; allocators do not use them for tie-breaking. |

---

## 5. Suggestions Beyond Status

1. **Prioritize PerGroup and GroupScalingBands** — These are core to modeling raids and group activities. Without them, activities with PerGroup attempts (e.g. CoX team loot) are effectively simulated incorrectly. Recommend implementing group formation and PerGroup roll handling before expanding strategy catalog.

2. **Add ActivityModifierRule to snapshot** — Low effort: extend TileActivityRuleSnapshotDto and EventSnapshotBuilder. Apply modifiers in SampleAttemptDuration and RollOutcome (probability). This will make capability-based advantages (e.g. quest unlocks) meaningful.

3. **Use WeeklySchedule for availability** — Simulation could advance sim time in coarse steps (e.g. hour) and only allow player attempts during their ScheduledSession windows. This would make skill and schedule trade-offs meaningful for strategy comparison.

4. **Expose metrics endpoint** — Add a simple `/api/metrics` or `/diagnostics` that returns current batch metrics (or last N batches). Helps with performance tuning without adding external systems.

5. **Per-run rerun UX** — When viewing sample run timelines, add "Rerun this run" that starts a batch of 1 with the same seed and run index. Requires storing run index or seed in the displayed sample.

6. **Worker concurrency** — Consider MassTransit prefetch or concurrent consumer configuration so a single Worker process can run multiple simulations in parallel. Would help single-host scaling before adding more containers.

7. **Failure modeling** — If desired, add outcome types that grant no progress but apply a time penalty (e.g. "death" outcome adds 60 seconds). Would require extending OutcomeSnapshotDto and SimulationRunner.

8. **Dry Run mode** — Add a checkbox "Single run (debug)" that sets run count to 1 and could enable more verbose logging. Optional but aligns with Flow 5.

---

## 6. Summary Table

| Category | Implemented | Partial | Missing |
|----------|-------------|---------|---------|
| Vision & success criteria | 5 | 0 | 0 |
| Scope — core concepts | 12 | 0 | 3 (play sessions, group tiles, failure modeling) |
| Domain entities | 18 | 3 (schedule, bands, modifiers) | 0 |
| User flows | 8 | 2 (dry run, refresh) | 0 |
| Architecture | 14 | 0 | 0 |
| Nonfunctional | 10 | 2 (metrics exposure, dry-run logging) | 0 |
| Acceptance scenarios | 18 | 4 (PerGroup/bands, rerun specific run, metrics, multi-worker validation) | 0 |

**Overall:** The application meets the walking skeleton and most v1 acceptance criteria. The principal gaps are in simulation fidelity (group play, schedule, modifiers, failure modeling) and in UX/observability (per-run rerun, metrics endpoint, dry-run toggle). Infrastructure (Docker, RabbitMQ, MassTransit, retries, finalization) is solid.

---

*End of audit.*
