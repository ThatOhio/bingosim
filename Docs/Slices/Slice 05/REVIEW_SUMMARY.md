# Slice 5: Simulation Execution — Review Summary

## Scope Delivered

- **Run Simulations UI:** Select event, view drafted teams and strategy configs, set run count, optional seed, execution mode (Local / Distributed stubbed), start batch (fire-and-forget); redirect to Results.
- **Local execution:** BackgroundService + channel; run work items enqueued on batch start; executor dequeues and runs simulation in-process with configurable concurrency (MaxConcurrentRuns) and optional delay (SimulationDelayMs).
- **Persistence:** SimulationBatch (Seed as string), EventSnapshot (EventConfigJson), SimulationRun (Seed as string, RunIndex, retry state), TeamRunResult (aggregates + RowUnlockTimesJson, TileCompletionTimesJson), BatchTeamAggregate (mean/min/max, winner rate).
- **Simulation engine (Application):** Snapshot builder; SeedDerivation (batch seed string + run index → RNG seed; run seed string for storage); RowUnlockHelper; IProgressAllocator (RowRush, GreedyPoints); SimulationRunner (event-based loop, duration and row unlock enforced).
- **Results UI:** Batch summary, progress counts (completed/failed/running/pending), metrics (RetryCount when &gt; 0, ElapsedSeconds, RunsPerSecond), per-team aggregates from BatchTeamAggregate, sample run timelines; **Rerun with same seed** button when batch is terminal; polling when batch running.
- **Retry:** Up to 5 attempts per run; failed run **re-enqueued** when AttemptCount &lt; 5; then run Failed, batch Error.
- **Observability:** ISimulationMetrics **implemented** (InMemorySimulationMetrics); BatchProgressResponse includes RetryCount, ElapsedSeconds, RunsPerSecond for throughput diagnosis.
- **Seeding:** Slice 4 teams/strategy added (Team Alpha RowRush, Team Beta GreedyPoints per seed event); reset order StrategyConfigs/Teams → Events → Activities → Players; DEV_SEEDING.md updated.

## Acceptance Alignment

| Doc Section | Status |
|-------------|--------|
| 5) Simulation Execution | ✅ Local execution, unlock rules, strategy allocation, retry/terminal failure |
| 6) Reproducibility & Seeds | ✅ Seed as string; derived RNG seed; same seed → same outcome (tested); **Rerun with same seed** on Results page |
| 7) Results & Aggregations | ✅ Stored aggregates (no recompute); timelines |
| 8) Observability | ✅ Structured logging; **ISimulationMetrics implemented**; progress shows RetryCount, Elapsed, Runs/sec; throttle knobs |
| 9) UI (Run + Results) | ✅ Run Simulations and Results pages; progress bar; metrics; rerun-by-seed; no hang |

## How to Run

- **Seeding:** `dotnet run --project BingoSim.Seed` (idempotent); `dotnet run --project BingoSim.Seed -- --reset` (reset seed data then reseed).
- **Web:** `dotnet run --project BingoSim.Web`; open Run Simulations, select event (with seeded teams), set run count and optional seed, start batch (Local), then view Results.
- **Tests:** `dotnet test` (full solution); or `dotnet test Tests/BingoSim.Application.UnitTests` and `dotnet test Tests/BingoSim.Infrastructure.IntegrationTests`.

## Files Changed (Summary)

- **Core:** New entities, enums, exceptions, repository interfaces (see SLICE5_COMPLETE.md).
- **Application:** Simulation (SeedDerivation, Snapshot DTOs + builder, Allocation, Runner), DTOs (BatchProgressResponse: RetryCount, ElapsedSeconds, RunsPerSecond), services (SimulationBatchService, SimulationRunExecutor with retry re-enqueue + metrics), interfaces (queue, metrics).
- **Infrastructure:** Configurations, migration, repositories, SimulationRunQueue, **InMemorySimulationMetrics**; DependencyInjection registers ISimulationMetrics.
- **Web:** RunSimulations.razor, SimulationResults.razor (metrics display, **Rerun with same seed**), SimulationRunQueueHostedService, LocalSimulationOptions, Program.cs, MainLayout.razor, appsettings.json.
- **Seeding:** DevSeedService (SeedTeamsAsync, reset order), DEV_SEEDING.md (reset order wording).
- **Tests:** RowUnlockHelperTests, RowRushAllocatorTests, GreedyPointsAllocatorTests, SeedDerivationTests, SimulationRunnerReproducibilityTests; **SimulationBatchServiceTests** (GetProgressAsync RetryCount/Elapsed/RunsPerSecond); SimulationBatchIntegrationTests; DevSeedServiceTests updated.
