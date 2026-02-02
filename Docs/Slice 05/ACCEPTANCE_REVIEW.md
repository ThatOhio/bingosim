# Acceptance Test Review - Simulation Execution (Slice 5)

## Review Date
2026-02-02

## Source
`Docs/06_Acceptance_Tests.md` — Sections 5 (Simulation Execution), 6 (Reproducibility & Seeds), 7 (Results & Aggregations), 8 (Observability & Performance Testability), 9 (UI Expectations: Run Simulations + Results)

---

## Acceptance Criteria Verification

### ✅ Section 5: Simulation Execution

**Local execution (internal worker):**
- [x] Start batch of N runs; batch completes without external worker containers
- [x] Results persisted and viewable
- [x] Run Simulations page: select Event, see drafted Teams and strategy configs, set run count, execution mode, optional seed, start batch (fire-and-forget)
- [x] Local executor runs runs in-process with configurable concurrency; UI remains responsive

**Unlock rules enforced (row rush):**
- [x] Row 0 unlocked at time 0; row N unlocks when ≥ 5 points completed in row N−1
- [x] Unlocks monotonic; never revert

**Strategy controls progress allocation:**
- [x] RowRush: prefer lowest row, then lowest points; deterministic tie-break
- [x] GreedyPoints: prefer highest points, then lowest row; deterministic tie-break
- [x] Progress applied only to eligible/unlocked tiles; multiple tiles in progress concurrently

**Retries and terminal failure:**
- [x] Retry failed runs up to 5 attempts; attempt count stored
- [x] After 5 failures: run marked Failed, batch marked Error; UI shows high-level error message

### ✅ Section 6: Reproducibility & Seeds

- [x] Seed stored as string on SimulationBatch and SimulationRun for reproducibility and UI display
- [x] RNG seed derived deterministically in Application from (BatchSeedString + RunIndex)
- [x] Same seed + same config → identical run results (reproducibility test in Application.UnitTests)

### ✅ Section 7: Results & Aggregations

- [x] Batch-level aggregates persisted (BatchTeamAggregate): mean/min/max points, tiles completed, row reached; winner rate
- [x] UI does not recompute aggregates; reads from BatchTeamAggregate
- [x] Timelines: row unlock times and tile completion times stored per run (JSON on TeamRunResult); displayed on Results page

### ✅ Section 8: Observability & Performance Testability

- [x] Structured logging includes BatchId and RunId
- [x] Basic metrics interface (ISimulationMetrics) for runs completed/failed/retried, batch duration (implementation can plug in System.Diagnostics.Metrics later)
- [x] LocalSimulationOptions: MaxConcurrentRuns, SimulationDelayMs for test-mode throttle

### ✅ Section 9: UI Expectations

- [x] Run Simulations: select event, see teams/strategies, run count, seed (optional), execution mode (Local / Distributed), start batch → redirect to Results
- [x] Simulation Results: view one batch; progress (completed/failed/running/pending); per-team aggregates when batch complete; sample run timelines; polling when running

---

## Implementation Summary

| Area | Implementation |
|------|----------------|
| **Core** | SimulationBatch, EventSnapshot, SimulationRun, TeamRunResult, BatchTeamAggregate; BatchStatus, RunStatus, ExecutionMode; repository interfaces |
| **Application** | SeedDerivation (string seed + run index → RNG seed; run seed string); EventSnapshotDto + EventSnapshotBuilder; IProgressAllocator + RowRushAllocator, GreedyPointsAllocator; SimulationRunner; StartSimulationBatch, SimulationRunExecutor; ISimulationRunQueue, ISimulationBatchService |
| **Infrastructure** | EF configs + migration AddSimulationBatchAndRuns; SimulationRunQueue (channel); repositories for batch, snapshot, run, team result, batch aggregate |
| **Web** | Run Simulations page (`/simulations/run`); Simulation Results page (`/simulations/results/{BatchId}`); SimulationRunQueueHostedService; LocalSimulationOptions; nav link |
| **Seeding** | DevSeedService extended: SeedTeamsAsync (Team Alpha RowRush, Team Beta GreedyPoints per seed event); reset order: Teams → Events → Activities → Players; DEV_SEEDING.md updated |

---

## Test Coverage

- **Unit (Application):** RowUnlockHelperTests, RowRushAllocatorTests, GreedyPointsAllocatorTests, SeedDerivationTests, SimulationRunnerReproducibilityTests (same seed → identical output)
- **Integration (Infrastructure):** SimulationBatchIntegrationTests (Postgres Testcontainers): create batch + snapshot + runs + results + aggregates; verify persistence
- **DevSeedServiceTests:** Updated for ITeamRepository dependency

---

## Post-Review Notes

- **Distributed workers:** Not implemented; execution mode "Distributed" is stubbed (UI shows "Coming soon"). Local path is fully functional.
- **Metrics:** ISimulationMetrics interface present; concrete implementation (e.g. System.Diagnostics.Metrics counters) can be added in Infrastructure without changing Application.
