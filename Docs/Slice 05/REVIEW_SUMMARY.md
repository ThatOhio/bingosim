# Slice 5: Simulation Execution — Review Summary

## Scope Delivered

- **Run Simulations UI:** Select event, view drafted teams and strategy configs, set run count, optional seed, execution mode (Local / Distributed stubbed), start batch (fire-and-forget); redirect to Results.
- **Local execution:** BackgroundService + channel; run work items enqueued on batch start; executor dequeues and runs simulation in-process with configurable concurrency (MaxConcurrentRuns) and optional delay (SimulationDelayMs).
- **Persistence:** SimulationBatch (Seed as string), EventSnapshot (EventConfigJson), SimulationRun (Seed as string, RunIndex, retry state), TeamRunResult (aggregates + RowUnlockTimesJson, TileCompletionTimesJson), BatchTeamAggregate (mean/min/max, winner rate).
- **Simulation engine (Application):** Snapshot builder; SeedDerivation (batch seed string + run index → RNG seed; run seed string for storage); RowUnlockHelper; IProgressAllocator (RowRush, GreedyPoints); SimulationRunner (event-based loop, duration and row unlock enforced).
- **Results UI:** Batch summary, progress counts, per-team aggregates from BatchTeamAggregate, sample run timelines; polling when batch running.
- **Retry:** Up to 5 attempts per run; then run Failed, batch Error.
- **Seeding:** Slice 4 teams/strategy added (Team Alpha RowRush, Team Beta GreedyPoints per seed event); reset order StrategyConfigs/Teams → Events → Activities → Players; DEV_SEEDING.md updated.

## Acceptance Alignment

| Doc Section | Status |
|-------------|--------|
| 5) Simulation Execution | ✅ Local execution, unlock rules, strategy allocation, retry/terminal failure |
| 6) Reproducibility & Seeds | ✅ Seed as string; derived RNG seed; same seed → same outcome (tested) |
| 7) Results & Aggregations | ✅ Stored aggregates; timelines |
| 8) Observability | ✅ Structured logging (BatchId, RunId); metrics interface; throttle knobs |
| 9) UI (Run + Results) | ✅ Run Simulations and Results pages; no hang |

## How to Run

- **Seeding:** `dotnet run --project BingoSim.Seed` (idempotent); `dotnet run --project BingoSim.Seed -- --reset` (reset seed data then reseed).
- **Web:** `dotnet run --project BingoSim.Web`; open Run Simulations, select event (with seeded teams), set run count and optional seed, start batch (Local), then view Results.
- **Tests:** `dotnet test` (full solution); or `dotnet test Tests/BingoSim.Application.UnitTests` and `dotnet test Tests/BingoSim.Infrastructure.IntegrationTests`.

## Files Changed (Summary)

- **Core:** New entities, enums, exceptions, repository interfaces (see SLICE5_COMPLETE.md).
- **Application:** Simulation (SeedDerivation, Snapshot DTOs + builder, Allocation, Runner), DTOs, services (SimulationBatchService, SimulationRunExecutor), interfaces (queue, metrics).
- **Infrastructure:** Configurations, migration, repositories, SimulationRunQueue.
- **Web:** RunSimulations.razor, SimulationResults.razor, SimulationRunQueueHostedService, LocalSimulationOptions, Program.cs, MainLayout.razor, appsettings.json.
- **Seeding:** DevSeedService (SeedTeamsAsync, reset order), DEV_SEEDING.md.
- **Tests:** RowUnlockHelperTests, RowRushAllocatorTests, GreedyPointsAllocatorTests, SeedDerivationTests, SimulationRunnerReproducibilityTests; SimulationBatchIntegrationTests; DevSeedServiceTests updated.
