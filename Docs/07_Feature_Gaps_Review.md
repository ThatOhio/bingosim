# Feature Gaps Review

This document identifies features and behaviors that are specified in the main documentation (00_Vision.md through 06_Acceptance_Tests.md) but are missing or incomplete in the current implementation. This is an independent review based on code inspection, not on the Slice 0* documents.

---

## Executive Summary

The core walking skeleton is in place: CRUD for Players, Activities, Events, Teams; local batch execution; batch aggregates; row unlock and tile completion timelines. Several significant features are missing or partially implemented, particularly around **distributed workers**, **group play simulation**, **play-session behavior**, **capability modifiers in simulation**, and **results discovery**.

---

## 1. Vision & Scope Gaps

### 1.1 Play-Session Behavior (Scope § Players, 02_Domain § ScheduledSession)

**Spec:** Players "follow realistic play-session behavior (e.g. daily play windows, session duration)".

**Current state:** PlayerProfile stores WeeklySchedule and ScheduledSession. The simulation snapshot (`PlayerSnapshotDto`) includes only `PlayerId`, `Name`, `SkillTimeMultiplier`, `CapabilityKeys`. No schedule or session windows are passed to the simulation.

**Gap:** SimulationRunner models players as continuously available. There is no mapping of simulation time to calendar days or session windows; players effectively work 24/7 during the event.

---

### 1.2 Group Tiles & Group Play (Scope § Group Tiles, 02_Domain § GroupScalingRule)

**Spec:**
- "Some tiles require or allow group participation"
- "Group size may affect attempt duration and probability"
- "Large teams may form multiple independent groups to attempt the same tile concurrently"

**Current state:**
- ActivityDefinition has GroupScalingBands; the snapshot includes them.
- SimulationRunner treats every attempt as **per-player**. Each `SimEvent` is (simTime, teamIndex, playerIndex, activityId, attemptKey). There is no concept of group formation or group-size-based time/probability scaling.
- `SampleAttemptDuration` uses only `skillMultiplier`; it does not look up GroupScalingBands or group size.

**Gap:** Group play and group scaling are not implemented in the simulation engine.

---

### 1.3 Per-Group Roll Scope (02_Domain § ActivityAttemptDefinition, 06_Acceptance § 2)

**Spec:** ActivityAttemptDefinition has RollScope (PerPlayer | PerGroup). "A tile can accept DropKeys from this per-group attempt definition; simulation can attribute per-group grants to team progress allocation."

**Current state:** RollScope exists in the domain and snapshot (`AttemptSnapshotDto.RollScope`). SimulationRunner ignores it; all rolls are effectively per-player.

**Gap:** PerGroup roll scope is not implemented. Per-group outcomes (e.g., team-scoped rare rolls) are not simulated.

---

### 1.4 Failure Modeling (Scope § Failure Modeling)

**Spec:** "Tile attempts may fail and waste time. Failures may include penalties (e.g. death and recovery). Failures do not permanently block progress."

**Current state:** SimulationRunner always rolls an outcome; every attempt produces progress or nothing (via outcome weights). There is no explicit failure outcome that wastes extra time or applies penalties.

**Gap:** Failure modeling (failed attempts with time waste/penalties) is not implemented.

---

### 1.5 Eastern Time for Schedules (02_Domain § Time)

**Spec:** "All schedules are interpreted in Eastern Time (America/New_York)."

**Current state:** ScheduledSession uses `TimeOnly StartLocalTime` and `DayOfWeek` with no timezone specification. Since schedules are not yet used in simulation, this is latent.

**Gap:** No explicit Eastern Time handling for schedules; will matter once play-session behavior is implemented.

---

## 2. Domain Model Gaps

### 2.1 TileActivityRule.DropKeyWeights (02_Domain § TileActivityRule)

**Spec:** "DropKeyWeights: Dictionary<string, int>? — if multiple keys can progress the tile, weights guide 'best eligible tile' allocation."

**Current state:** TileActivityRule has AcceptedDropKeys, Requirements, Modifiers. No DropKeyWeights. AllocatorContext receives EligibleTileKeys but no per-tile or per-drop-key weights. Allocators (GreedyPoints, RowRush) use tile points and row index only.

**Gap:** DropKeyWeights for guiding allocation when multiple tiles accept the same DropKey are not implemented.

---

### 2.2 ActivityModifierRule in Simulation (02_Domain § ActivityModifierRule)

**Spec:** Modifiers apply TimeMultiplier and/or ProbabilityMultiplier when the player possesses the capability. "Eligibility is unaffected unless the capability is also listed as a Requirement."

**Current state:** TileActivityRule has Modifiers; EventCreate/EventEdit support them. `TileActivityRuleSnapshotDto` includes only `RequirementKeys`, not modifiers. SimulationRunner does not apply capability-based time or probability modifiers.

**Gap:** ActivityModifierRule is persisted and editable but not used during simulation.

---

### 2.3 SimulationBatch.Notes and CreatedBy (02_Domain § SimulationBatch)

**Spec:** SimulationBatch has optional `Notes` and `CreatedBy`.

**Current state:** SimulationBatch has `Name`, `RunsRequested`, `Seed`, `ExecutionMode`, `Status`, `ErrorMessage`, `CreatedAt`, `CompletedAt`. No Notes or CreatedBy.

**Gap:** Notes and CreatedBy are not in the domain or persistence.

---

### 2.4 Strategy "Balanced" (02_Domain § StrategyConfig, 03_User_Flows § Flow 4)

**Spec:** StrategyKey examples include "GreedyPoints", "RowRush", "Balanced".

**Current state:** StrategyCatalog only exposes RowRush and GreedyPoints.

**Gap:** Balanced strategy is referenced but not implemented.

---

## 3. User Flow & UI Gaps

### 3.1 Run Simulations — Batch Name Input (03_User_Flows § Flow 5)

**Spec:** User may provide a batch name when starting a batch.

**Current state:** StartSimulationBatchRequest has optional `Name`. RunSimulations.razor does not expose a batch name input.

**Gap:** Batch name cannot be set from the UI.

---

### 3.2 Run Simulations — Distributed Execution (03_User_Flows § Flow 5, 04_Architecture)

**Spec:** "Choose execution mode: Local execution (internal worker) or Distributed workers (external worker containers)."

**Current state:** RunSimulations shows "Local (in-process)" and "Distributed (coming soon)". Distributed mode is not implemented; the system uses an in-memory Channel-based queue, not MassTransit/RabbitMQ.

**Gap:** Distributed execution is not implemented.

---

### 3.3 Simulation Results — Batch List / Discovery (03_User_Flows § Flow 6, 06_Acceptance § 9)

**Spec:** "Simulation Results supports viewing one batch at a time with required metrics and timelines."

**Current state:** SimulationResults.razor requires a BatchId in the URL (`/simulations/results/{BatchId:guid}`). There is no list of batches; users can only reach results by being redirected after starting a batch or by manually entering a batch ID. MainLayout has "Run Simulations" but no "Simulation Results" or "Batches" link.

**Gap:** No way to browse or select completed batches. Users cannot discover past batches from the UI.

---

### 3.4 Rerun Specific Run (06_Acceptance § 6)

**Spec:** "Given a completed run has a recorded seed, when I request a re-run of that specific run (via UI action or run-details view), then the rerun produces identical results."

**Current state:** SimulationResults has "Rerun with same seed" which reruns the entire batch with the same seed, not a specific run. There is no run-details view or per-run rerun action.

**Gap:** Rerun of a single run (by run index) is not supported.

---

### 3.5 Primary Navigation — Simulation Results (03_User_Flows § Primary Navigation Areas)

**Spec:** Navigation areas include "Simulation Results".

**Current state:** MainLayout links to Home, Players, Activities, Events, Run Simulations. There is no direct link to Simulation Results or a batch list.

**Gap:** Simulation Results is not a top-level navigation target.

---

## 4. Architecture & Infrastructure Gaps

### 4.1 MassTransit / RabbitMQ (04_Architecture § Runtime Topology)

**Spec:** RabbitMQ as message broker; MassTransit for work orchestration. Web and workers connect to Postgres and RabbitMQ.

**Current state:** SimulationRunQueue is an in-memory Channel. No MassTransit, no RabbitMQ. compose.yaml has Postgres and two services (bingosim.web, bingosim.worker) but no RabbitMQ. BingoSim.Worker is a stub (prints "Hello, World!").

**Gap:** Distributed messaging and worker execution are not implemented.

---

### 4.2 Worker Completion of Batches Without UI (04_Architecture § Flow C, D)

**Spec:** Batches must complete without the Web UI being online. Workers drive batches to completion via message consumption.

**Current state:** Local execution uses an in-process hosted service and Channel. If the Web process stops, in-flight work is lost. Distributed workers are not implemented.

**Gap:** Fire-and-forget, UI-independent batch completion is only possible with distributed workers.

---

### 4.3 Idempotency and At-Least-Once Delivery (05_Nonfunctional § At-Least-Once)

**Spec:** "Workers must treat work as idempotent by claiming work via DB-backed state transitions (locking/compare-and-swap semantics) and detecting 'late' duplicate work."

**Current state:** SimulationRunExecutor checks `run.IsTerminal` before executing. There is no explicit compare-and-swap or locking when transitioning from Pending to Running. Duplicate messages (if they existed) could lead to duplicate execution.

**Gap:** Idempotency and duplicate-work handling may need strengthening for distributed execution.

---

## 5. Non-Functional & Observability Gaps

### 5.1 Metrics Endpoint / Page (05_Nonfunctional § Metrics, 06_Acceptance § 8)

**Spec:** Minimum metrics: runs completed, failed, retried, currently running; batch duration; throughput (runs/sec). "Metrics output may be structured logs or a simple in-app endpoint/page."

**Current state:** InMemorySimulationMetrics records counts; BatchProgressResponse derives progress from DB (completed, failed, running, pending). The Simulation Results page shows progress (including RetryCount, ElapsedSeconds, RunsPerSecond) but there is no dedicated metrics/diagnostics endpoint or page for performance tuning across batches or workers.

**Gap:** No dedicated metrics endpoint or diagnostics page; metrics are embedded in batch results only.

---

### 5.2 Multi-Worker Parallelism Validation (06_Acceptance § 8)

**Spec:** "Configuration exists to limit worker concurrency or artificially add delay (test mode). Two distributed workers running; batch with test-mode throttling; observed throughput improves with two workers vs one."

**Current state:** LocalSimulationOptions has `SimulationDelayMs` and `MaxConcurrentRuns`. These apply to the in-process hosted service only. There are no distributed workers to compare.

**Gap:** Multi-worker parallelism cannot be validated because distributed workers are not implemented.

---

### 5.3 Docker Compose — RabbitMQ (04_Architecture, 05_Nonfunctional)

**Spec:** Workers coordinate via shared Postgres and RabbitMQ.

**Current state:** compose.yaml does not include RabbitMQ.

**Gap:** Compose stack is incomplete for distributed execution.

---

## 6. Acceptance Test Coverage Gaps

### 6.1 Walking Skeleton — Distributed Workers (06_Acceptance § 0, 5)

**Spec:** "The system is running with Postgres and RabbitMQ available"; "Run using distributed workers"; "Workers execute and persist results; Web UI can be closed without stopping execution."

**Current state:** RabbitMQ and distributed workers are not implemented.

---

### 6.2 Retries and Terminal Failure — UI Error Display (06_Acceptance § 5)

**Spec:** "If it still fails after 5 attempts: run is marked Failed, batch is marked Error, UI shows that the batch errored and why (high-level message)."

**Current state:** SimulationRunExecutor marks runs Failed and batch Error with a message. SimulationResults displays ErrorMessage when Status is Error. This appears implemented.

---

### 6.3 Reproducibility — Rerun Specific Run (06_Acceptance § 6)

**Spec:** "I request a re-run of that specific run (via UI action or run-details view). The rerun produces identical results."

**Current state:** Only whole-batch rerun with same seed exists.

---

### 6.4 Results — Batch-Level Aggregates Precomputed (06_Acceptance § 7)

**Spec:** "The UI does not recompute aggregates from scratch each time."

**Current state:** BatchTeamAggregate is persisted; SimulationResults loads aggregates from DB. Implemented.

---

## 7. Summary Table

| Category                | Feature                              | Status        | Priority |
|-------------------------|--------------------------------------|---------------|----------|
| Simulation              | Play-session behavior (schedules)    | Missing       | High     |
| Simulation              | Group play & scaling                 | Missing       | High     |
| Simulation              | Per-group roll scope                 | Missing       | High     |
| Simulation              | Failure modeling                     | Missing       | Medium   |
| Simulation              | ActivityModifierRule in sim          | Missing       | Medium   |
| Domain                  | DropKeyWeights                       | Missing       | Low      |
| Domain                  | Batch Notes / CreatedBy              | Missing       | Low      |
| Domain                  | Balanced strategy                    | Missing       | Low      |
| UI                      | Batch name input                     | Missing       | Low      |
| UI                      | Batch list / results discovery       | Missing       | High     |
| UI                      | Rerun specific run                   | Missing       | Medium   |
| UI                      | Simulation Results nav link          | Missing       | Medium   |
| Infrastructure          | MassTransit / RabbitMQ               | Missing       | High     |
| Infrastructure          | Distributed workers                  | Missing       | High     |
| Infrastructure          | RabbitMQ in compose                  | Missing       | High     |
| Observability           | Dedicated metrics endpoint           | Partial       | Medium   |
| Non-functional          | Idempotency for distributed runs     | Needs review  | High     |

---

## 8. Recommended Implementation Order

1. **Simulation Results discovery** — Add a batch list page and nav link so users can access past batches.
2. **Distributed workers** — Add RabbitMQ to compose, implement MassTransit consumers in BingoSim.Worker, enable distributed execution mode.
3. **Group play & scaling** — Model group formation and use GroupScalingBands in simulation.
4. **Play-session behavior** — Include schedule in snapshot and constrain player availability by session windows.
5. **ActivityModifierRule in simulation** — Add modifiers to snapshot and apply time/probability multipliers.
6. **Rerun specific run** — Add run-details view and per-run rerun action.
7. **Remaining gaps** — Failure modeling, DropKeyWeights, Balanced strategy, batch Notes/CreatedBy, batch name input, metrics endpoint, Eastern Time for schedules.

---

*Document generated from codebase review against 00_Vision.md, 01_Scope.md, 02_Domain.md, 03_User_Flows.md, 04_Architecture.md, 05_Nonfunctional.md, and 06_Acceptance_Tests.md.*
