# Requirements Review: Implementation vs. Specification

**Review Date:** February 2, 2025  
**Scope:** Code implementation compared against 00_Vision.md, 01_Scope.md, 02_Domain.md, 03_User_Flows.md, 04_Architecture.md, 05_Nonfunctional.md, and 06_Acceptance_Tests.md  
**Method:** Direct code inspection and comparison with specification documents

---

## 1. Executive Summary

The BingoSim application has achieved **substantial alignment** with its specification. The walking skeleton is complete: end-to-end flow from libraries → event → teams → batch → results works for both local and distributed execution. Recent slices (7–10) have closed many gaps identified in earlier reviews: distributed workers, batch discovery, group play, play-session behavior, and ActivityModifierRule are now implemented.

**Remaining gaps** are primarily in simulation fidelity (failure modeling, DropKeyWeights), optional metadata (Notes, CreatedBy), UX polish (batch name input, per-run rerun, dry-run toggle), and observability (dedicated metrics endpoint). The "Balanced" strategy referenced in the domain is not implemented.

---

## 2. Document-by-Document Assessment

### 2.1 Vision (00_Vision.md)

| Success Criterion | Status | Notes |
|-------------------|--------|-------|
| Web UI configures and launches full simulated community event | ✅ | Run Simulations page; event, teams, run count, seed, execution mode |
| One or more worker processes execute simulations to completion | ✅ | Local (in-process) and distributed (RabbitMQ + Worker) |
| Event simulated start-to-finish without manual intervention | ✅ | Batch orchestration, run creation, execution, finalization |
| Simulation results inspected and compared across strategies | ✅ | Per-team aggregates, winner rate, mean/min/max, timelines |
| Single-user, no multi-tenant | ✅ | No auth; internal tool |

**Verdict:** All vision success criteria are met.

---

### 2.2 Scope (01_Scope.md)

| Concept | Status | Notes |
|---------|--------|-------|
| **Players** — Fixed skill, capabilities | ✅ | PlayerProfile: SkillTimeMultiplier, Capabilities |
| **Players** — Realistic play-session behavior | ✅ | WeeklySchedule, ScheduleEvaluator; players act only during online sessions (Slice 10) |
| **Event Teams** — Groups with shared strategy | ✅ | Team, StrategyConfig, TeamPlayer |
| **Events** — Fixed duration, rules at start | ✅ | Event.Duration, UnlockPointsRequiredPerRow |
| **Event Board** — Ordered rows, 4 tiles (1–4 pts) per row | ✅ | Row.Index, Tile.Points |
| **Tile Unlock** — ≥5 pts from previous row | ✅ | RowUnlockHelper; monotonic unlocks |
| **Probabilistic time-based tiles** | ✅ | AttemptTimeModel, outcomes with weights |
| **Eligibility & Requirements** | ✅ | TileActivityRule.Requirements; GetFirstEligibleActivity checks capabilities |
| **Skill effects on time** | ✅ | SkillTimeMultiplier applied in SampleAttemptDuration |
| **Group Tiles** | ✅ | Group formation, PerGroup/PerPlayer rolls, GroupScalingBands (Slice 9) |
| **Failure modeling** | ❌ | No explicit failure outcomes with time penalties |
| **Strategy & decision making** | ✅ | RowRush, GreedyPoints; allocator selects target tile |
| **Execution model** — Workers, thousands to millions | ✅ | Local + distributed; configurable concurrency |
| **Persistence** | ✅ | Postgres; EventSnapshot JSON; TeamRunResult; BatchTeamAggregate |

**Verdict:** One scope gap: failure modeling (tile attempts that fail and waste time, e.g. death and recovery).

---

### 2.3 Domain (02_Domain.md)

| Entity/Concept | Status | Notes |
|-----------------|--------|-------|
| Capability | ✅ | Value object; PlayerProfile has many |
| PlayerProfile | ✅ | Name, SkillTimeMultiplier, Capabilities, WeeklySchedule |
| WeeklySchedule, ScheduledSession | ✅ | Stored, editable; used in simulation (Slice 10) |
| ActivityDefinition | ✅ | Key, Name, ModeSupport, Attempts, GroupScalingBands |
| ActivityModeSupport | ✅ | SupportsSolo, SupportsGroup, Min/MaxGroupSize |
| ActivityAttemptDefinition | ✅ | Key, RollScope (PerPlayer/PerGroup), TimeModel, Outcomes |
| AttemptTimeModel | ✅ | BaselineTimeSeconds, Distribution, VarianceSeconds |
| ActivityOutcomeDefinition, ProgressGrant | ✅ | WeightNumerator/Denominator; Grants with DropKey, Units |
| GroupScalingRule / GroupSizeBand | ✅ | Stored; applied in simulation via GroupScalingBandSelector |
| Event, Row, Tile | ✅ | Duration, Rows, Tiles with Points 1–4 |
| TileActivityRule | ✅ | Activity, AcceptedDropKeys, Requirements, Modifiers |
| TileActivityRule.DropKeyWeights | ❌ | Not in snapshot or allocator logic |
| ActivityModifierRule | ✅ | Stored; in snapshot; applied in simulation (Slice 8) |
| Team, TeamPlayer, StrategyConfig | ✅ | Per-event teams; StrategyKey, ParamsJson |
| TeamRowUnlockState, TeamTileProgress | ✅ | Modeled in TeamRunState during run |
| SimulationBatch | ⚠️ | Has Name; no Notes, CreatedBy (spec allows optional) |
| SimulationRun, EventSnapshot | ✅ | Batch, runs, snapshot JSON |
| TeamSimulationResult | ✅ | TeamRunResult: TotalPoints, TilesCompletedCount, RowReached, IsWinner, timelines |
| BatchTeamAggregate | ✅ | Mean/min/max points, tiles, row; WinnerRate |
| StrategyKey examples (GreedyPoints, RowRush, **Balanced**) | ⚠️ | Balanced not implemented |

**Verdict:** Minor gaps: DropKeyWeights, SimulationBatch.Notes/CreatedBy, Balanced strategy.

---

### 2.4 User Flows (03_User_Flows.md)

| Flow | Status | Notes |
|------|--------|-------|
| **Flow 1:** Create/Update Player Library | ✅ | Players CRUD; name, skill, capabilities, weekly schedule |
| **Flow 2:** Create/Update Activity Library | ✅ | Activities CRUD; attempts, outcomes, group scaling bands |
| **Flow 3:** Create Event and Board | ✅ | Events CRUD; rows, tiles, TileActivityRules |
| **Flow 4:** Assign Teams and Strategies | ✅ | Event teams page; create team, assign players, StrategyKey + ParamsJson |
| **Flow 5:** Run Simulations (Batch) | ✅ | Select event, teams, run count, seed, execution mode (Local/Distributed) |
| **Flow 5:** Batch name input | ❌ | StartSimulationBatchRequest has Name; RunSimulations.razor does not expose it |
| **Flow 5:** Dry Run / Single-Run mode | ⚠️ | User can set run count = 1; no explicit "Dry Run" toggle or extra logging |
| **Flow 6:** View Simulation Results | ✅ | Batches list at /simulations/results; per-batch results; aggregates; timelines |
| Primary Navigation — Simulation Results | ✅ | MainLayout links to /simulations/results |
| Delete confirmation (Players, Activities, Events, Teams) | ✅ | DeleteConfirmationModal used |
| No dashboard (v1) | ✅ | Home is minimal; nav to Players, Activities, Events, Run, Results |

**Verdict:** One flow gap: batch name input. Dry-run is workable but not explicit.

---

### 2.5 Architecture (04_Architecture.md)

| Component | Status | Notes |
|-----------|--------|-------|
| BingoSim.Core — domain, no infra | ✅ | Entities, value objects, interfaces |
| BingoSim.Application — use cases | ✅ | Services, DTOs, simulation runner, allocators |
| BingoSim.Infrastructure — adapters | ✅ | EF Core, repos, MassTransit, BatchFinalization |
| BingoSim.Web — presentation | ✅ | Blazor Server; Run Simulations, Results |
| BingoSim.Worker — execution | ✅ | MassTransit consumer; ExecuteSimulationRun |
| Docker topology — Postgres, RabbitMQ, Web, Worker | ✅ | compose.yaml includes RabbitMQ |
| Snapshot-at-start for reproducibility | ✅ | EventSnapshotBuilder; JSON stored with batch |
| Fire-and-forget messaging | ✅ | Web publishes; workers consume; UI polls DB |
| Optional local execution | ✅ | SimulationRunQueueHostedService; LocalSimulationOptions |
| Parallel workers, no ordering | ✅ | MassTransit; TryClaimAsync for idempotency |
| SimulationBatchStarted, ExecuteSimulationRun | ✅ | Shared.Messages contracts |
| Failure metadata (AttemptCount, LastError, Status) | ✅ | SimulationRun |
| Batch completion when all runs terminal | ✅ | BatchFinalizationService; TryTransitionToFinalAsync |
| Worker MaxConcurrentRuns | ⚠️ | Local executor has MaxConcurrentRuns; Worker consumes one message at a time |

**Verdict:** Architecture fully aligned. Optional improvement: Worker-level concurrency for single-host scaling.

---

### 2.6 Nonfunctional (05_Nonfunctional.md)

| Requirement | Status | Notes |
|-------------|--------|-------|
| Retry up to 5 attempts | ✅ | SimulationRun.MarkFailed; AttemptCount >= 5 → Failed |
| Batch surfaced as error on terminal failures | ✅ | BatchStatus.Error; ErrorMessage |
| At-least-once; DB source of truth | ✅ | TryClaimAsync; idempotent execution |
| Workers independent of Web UI | ✅ | Distributed mode; Web can close |
| Batch-level aggregates stored | ✅ | BatchTeamAggregate; winner rate, mean/min/max |
| Structured logging (BatchId, RunId) | ✅ | Logger calls in executor, consumer |
| Dry-run verbose logging | ⚠️ | Not explicitly toggled by run count |
| Metrics: runs completed/failed/retried, batch duration, throughput | ⚠️ | Computed in GetProgressAsync for UI; no dedicated endpoint |
| RNG seed recorded per run | ✅ | SimulationRun.Seed; stored in batch |
| Snapshot reproducibility | ✅ | EventSnapshot JSON at batch start |
| No auth (internal-only) | ✅ | No auth middleware |
| Linux x86_64, Docker Compose | ✅ | Dockerfiles; compose.yaml |

**Verdict:** Core nonfunctional requirements met. Metrics exist in batch progress but no dedicated /metrics or diagnostics page for programmatic inspection.

---

### 2.7 Acceptance Tests (06_Acceptance_Tests.md)

| Scenario | Status | Notes |
|----------|--------|-------|
| **0) Walking Skeleton** — Create libs, event, run batch, view results | ✅ | Full flow supported |
| **1) Player Library** — Create, Edit, Delete with confirmation | ✅ | CRUD + DeleteConfirmationModal |
| **2) Activity Library** — Multiple loot lines, PerGroup rare roll, Group scaling bands | ✅ | CRUD; PerGroup and bands used in sim |
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
| **8) Metrics for throughput diagnosis** | ⚠️ | RetryCount, Elapsed, Runs/sec in batch UI; no dedicated metrics page |
| **8) Multi-worker parallelism validation** | ⚠️ | Can run multiple Worker containers; no test-mode throttling config for distributed |
| **9) All pages CRUD** | ✅ | Players, Activities, Events with create/edit/delete; Run Simulations; Results |

**Verdict:** One acceptance gap: rerun specific run. Metrics and multi-worker validation are partial.

---

## 3. Gap Summary

### 3.1 High Priority (Spec-Explicit Gaps)

| Gap | Spec Reference | Current State |
|-----|----------------|---------------|
| **Rerun specific run** | 06_Acceptance § 6 | Only whole-batch rerun; no run-details view or per-run rerun action |
| **Failure modeling** | 01_Scope § Failure Modeling | No explicit failure outcomes with time penalties (e.g. death and recovery) |

### 3.2 Medium Priority (UX / Observability)

| Gap | Spec Reference | Current State |
|-----|----------------|---------------|
| **Batch name input** | 03_User_Flows § Flow 5; StartSimulationBatchRequest | Request supports Name; RunSimulations.razor does not expose input |
| **Metrics endpoint/page** | 05_Nonfunctional § Metrics; 06_Acceptance § 8 | Batch progress shows metrics; no dedicated /metrics or diagnostics page |
| **Dry Run toggle** | 03_User_Flows § Flow 5 | User can set run count = 1; no explicit "Dry Run" mode with extra logging |

### 3.3 Lower Priority (Optional / Domain Polish)

| Gap | Spec Reference | Current State |
|-----|----------------|---------------|
| **DropKeyWeights** | 02_Domain § TileActivityRule | Not in snapshot or allocator; allocation uses tile points and row only |
| **Balanced strategy** | 02_Domain § StrategyConfig; 03_User_Flows § Flow 4 | StrategyCatalog has RowRush, GreedyPoints only |
| **SimulationBatch Notes, CreatedBy** | 02_Domain § SimulationBatch | Entity has Name; no Notes or CreatedBy |
| **Worker MaxConcurrentRuns** | 04_Architecture § Concurrency | Local has MaxConcurrentRuns; Worker consumes one message at a time |

---

## 4. Suggested Improvements (Beyond Spec)

### 4.1 Code Quality & Architecture

- **Event.StartTime:** Domain could add `Event.StartTime` (nullable) for explicit event start instead of deriving from `batch.CreatedAt`. Would allow users to specify "event starts Monday 00:00 ET" independent of when the batch is created.
- **Strategy extensibility:** Consider a plugin-style strategy registration so new strategies (e.g. Balanced) can be added without modifying StrategyCatalog.
- **Snapshot versioning:** EventSnapshot JSON has no version field. Adding a schema version would ease future migrations if snapshot structure changes.

### 4.2 UX Enhancements

- **Batch list improvements:** Add pagination or "load more" for large batch lists; show batch name (when set) in list; allow sorting by CreatedAt, Status.
- **Sample run selection:** Allow user to pick which run (e.g. run index 0, 1, …) to view timelines for, not just the first.
- **Event name in batch list:** Already shown; consider making it a link to the event edit page.
- **Copy seed to clipboard:** On results page, add "Copy seed" for reproducibility.

### 4.3 Observability & Operations

- **Health endpoint:** Add `/health` or `/ready` for container orchestration and load balancers.
- **Structured metrics export:** Expose Prometheus or OpenTelemetry metrics for runs completed, failed, retried, batch duration, throughput.
- **Batch cleanup:** Document or provide a script for manual retention (e.g. delete batches older than N days) per 05_Nonfunctional § Retention Policy.

### 4.4 Testing & Quality

- **Acceptance test automation:** Consider automated E2E tests (e.g. Playwright) for the walking skeleton and key flows.
- **Performance benchmarks:** Add benchmark tests for simulation throughput (runs/sec) to catch regressions.
- **Snapshot backward compatibility:** Add tests that old snapshot JSON (without EventStartTimeEt, Schedule) still deserializes and runs correctly.

### 4.5 Documentation

- **API documentation:** If REST endpoints are added, document them (OpenAPI/Swagger).
- **Deployment runbook:** Document steps for Docker Compose deployment, env vars, and troubleshooting.
- **Strategy parameter docs:** Document JSON schema or examples for each strategy's ParamsJson.

---

## 5. Implementation Status Table

| Category | Implemented | Partial | Missing |
|----------|-------------|---------|---------|
| Vision & success criteria | 5 | 0 | 0 |
| Scope — core concepts | 13 | 0 | 1 (failure modeling) |
| Domain entities | 20 | 2 (DropKeyWeights, Notes/CreatedBy) | 1 (Balanced) |
| User flows | 9 | 2 (batch name, dry run) | 0 |
| Architecture | 14 | 1 (Worker concurrency) | 0 |
| Nonfunctional | 10 | 2 (metrics exposure, dry-run logging) | 0 |
| Acceptance scenarios | 19 | 2 (metrics, multi-worker validation) | 1 (rerun specific run) |

---

## 6. Recommended Implementation Order

1. **Batch name input** — Low effort; add optional Name field to RunSimulations.razor; pass to StartSimulationBatchRequest.
2. **Rerun specific run** — Add run-details view (or expand sample run section); "Rerun this run" starts batch of 1 with same seed and run index.
3. **Metrics endpoint** — Add `/api/metrics` or `/diagnostics` returning current/last batch metrics (runs completed, failed, retried, throughput).
4. **Dry Run toggle** — Checkbox "Single run (debug)" that sets run count to 1 and optionally enables verbose logging.
5. **Failure modeling** — Add outcome type that grants no progress but applies time penalty; extend OutcomeSnapshotDto and SimulationRunner.
6. **DropKeyWeights** — Add to TileActivityRule, snapshot, and allocator context for tie-breaking when multiple tiles accept same DropKey.
7. **Balanced strategy** — Implement and add to StrategyCatalog if desired.
8. **SimulationBatch Notes, CreatedBy** — Add optional columns and UI if needed.
9. **Worker MaxConcurrentRuns** — Configure MassTransit prefetch or concurrent consumers for single-host parallelism.

---

## 7. Conclusion

The BingoSim implementation is **well-aligned** with its specification. The walking skeleton, distributed execution, group play, play-session behavior, and capability modifiers are in place. Remaining gaps are mostly optional polish (batch name, dry-run toggle, Notes/CreatedBy), one acceptance criterion (rerun specific run), and one scope item (failure modeling). The suggested improvements focus on UX, observability, and maintainability rather than core functionality.

---

*Document generated from codebase review against 00_Vision.md, 01_Scope.md, 02_Domain.md, 03_User_Flows.md, 04_Architecture.md, 05_Nonfunctional.md, and 06_Acceptance_Tests.md.*
