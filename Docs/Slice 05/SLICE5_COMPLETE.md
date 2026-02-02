# Slice 5: Simulation Execution (Local Execution First) — Complete

## Delivered Scope

- Run Simulations page: select Event, display drafted Teams and strategy configs, set run count, execution mode (Local / Distributed stubbed), optional seed, start batch (fire-and-forget).
- Local executor: BackgroundService + channel; runs executed in-process with configurable concurrency and optional delay; UI remains responsive.
- Persistence: SimulationBatch (Seed string), EventSnapshot, SimulationRun (Seed string, RunIndex, retry), TeamRunResult (aggregates + timeline JSON), BatchTeamAggregate.
- Simulation engine in Application: snapshot builder, seed derivation (batch seed string + run index → RNG seed; run seed string stored), row unlock helper, RowRush/GreedyPoints allocators, SimulationRunner.
- Results page: batch summary, progress (completed/failed/running/pending), per-team aggregates, sample run timelines; polling when running.
- Retry: up to 5 attempts per run; then run Failed, batch Error.
- Observability: structured logging (BatchId, RunId); ISimulationMetrics; LocalSimulationOptions (MaxConcurrentRuns, SimulationDelayMs).
- Dev seeding: Slice 4 teams/strategy (Team Alpha RowRush, Team Beta GreedyPoints per seed event); reset order updated; DEV_SEEDING.md updated.

---

## File List Changed

### Created

**Core**
- `BingoSim.Core/Enums/BatchStatus.cs`
- `BingoSim.Core/Enums/RunStatus.cs`
- `BingoSim.Core/Enums/ExecutionMode.cs`
- `BingoSim.Core/Entities/SimulationBatch.cs`
- `BingoSim.Core/Entities/EventSnapshot.cs`
- `BingoSim.Core/Entities/SimulationRun.cs`
- `BingoSim.Core/Entities/TeamRunResult.cs`
- `BingoSim.Core/Entities/BatchTeamAggregate.cs`
- `BingoSim.Core/Exceptions/SimulationBatchNotFoundException.cs`
- `BingoSim.Core/Interfaces/ISimulationBatchRepository.cs`
- `BingoSim.Core/Interfaces/IEventSnapshotRepository.cs`
- `BingoSim.Core/Interfaces/ISimulationRunRepository.cs`
- `BingoSim.Core/Interfaces/ITeamRunResultRepository.cs`
- `BingoSim.Core/Interfaces/IBatchTeamAggregateRepository.cs`

**Application**
- `BingoSim.Application/Simulation/SeedDerivation.cs`
- `BingoSim.Application/Simulation/Snapshot/EventSnapshotDto.cs`
- `BingoSim.Application/Simulation/Snapshot/RowSnapshotDto.cs`
- `BingoSim.Application/Simulation/Snapshot/TileSnapshotDto.cs`
- `BingoSim.Application/Simulation/Snapshot/TileActivityRuleSnapshotDto.cs`
- `BingoSim.Application/Simulation/Snapshot/ActivitySnapshotDto.cs`
- `BingoSim.Application/Simulation/Snapshot/AttemptSnapshotDto.cs`
- `BingoSim.Application/Simulation/Snapshot/OutcomeSnapshotDto.cs`
- `BingoSim.Application/Simulation/Snapshot/ProgressGrantSnapshotDto.cs`
- `BingoSim.Application/Simulation/Snapshot/GroupSizeBandSnapshotDto.cs`
- `BingoSim.Application/Simulation/Snapshot/TeamSnapshotDto.cs`
- `BingoSim.Application/Simulation/Snapshot/PlayerSnapshotDto.cs`
- `BingoSim.Application/Simulation/Snapshot/EventSnapshotBuilder.cs`
- `BingoSim.Application/Simulation/Allocation/AllocatorContext.cs`
- `BingoSim.Application/Simulation/Allocation/IProgressAllocator.cs`
- `BingoSim.Application/Simulation/Allocation/RowRushAllocator.cs`
- `BingoSim.Application/Simulation/Allocation/GreedyPointsAllocator.cs`
- `BingoSim.Application/Simulation/Allocation/IProgressAllocatorFactory.cs`
- `BingoSim.Application/Simulation/Allocation/ProgressAllocatorFactory.cs`
- `BingoSim.Application/Simulation/Runner/RowUnlockHelper.cs`
- `BingoSim.Application/Simulation/Runner/TeamRunState.cs`
- `BingoSim.Application/Simulation/Runner/TeamRunResultDto.cs`
- `BingoSim.Application/Simulation/Runner/SimulationRunner.cs`
- `BingoSim.Application/DTOs/StartSimulationBatchRequest.cs`
- `BingoSim.Application/DTOs/SimulationBatchResponse.cs`
- `BingoSim.Application/DTOs/BatchProgressResponse.cs`
- `BingoSim.Application/DTOs/BatchTeamAggregateResponse.cs`
- `BingoSim.Application/DTOs/TeamRunResultResponse.cs`
- `BingoSim.Application/Interfaces/ISimulationBatchService.cs`
- `BingoSim.Application/Interfaces/ISimulationRunQueue.cs`
- `BingoSim.Application/Interfaces/ISimulationRunExecutor.cs`
- `BingoSim.Application/Interfaces/ISimulationMetrics.cs`
- `BingoSim.Application/Services/SimulationBatchService.cs`
- `BingoSim.Application/Services/SimulationRunExecutor.cs`

**Infrastructure**
- `BingoSim.Infrastructure/Persistence/Configurations/SimulationBatchConfiguration.cs`
- `BingoSim.Infrastructure/Persistence/Configurations/EventSnapshotConfiguration.cs`
- `BingoSim.Infrastructure/Persistence/Configurations/SimulationRunConfiguration.cs`
- `BingoSim.Infrastructure/Persistence/Configurations/TeamRunResultConfiguration.cs`
- `BingoSim.Infrastructure/Persistence/Configurations/BatchTeamAggregateConfiguration.cs`
- `BingoSim.Infrastructure/Persistence/Repositories/SimulationBatchRepository.cs`
- `BingoSim.Infrastructure/Persistence/Repositories/EventSnapshotRepository.cs`
- `BingoSim.Infrastructure/Persistence/Repositories/SimulationRunRepository.cs`
- `BingoSim.Infrastructure/Persistence/Repositories/TeamRunResultRepository.cs`
- `BingoSim.Infrastructure/Persistence/Repositories/BatchTeamAggregateRepository.cs`
- `BingoSim.Infrastructure/Simulation/SimulationRunQueue.cs`
- `BingoSim.Infrastructure/Persistence/Migrations/20260202020256_AddSimulationBatchAndRuns.cs`
- `BingoSim.Infrastructure/Persistence/Migrations/20260202020256_AddSimulationBatchAndRuns.Designer.cs`

**Web**
- `BingoSim.Web/Components/Pages/Simulations/RunSimulations.razor`
- `BingoSim.Web/Components/Pages/Simulations/RunSimulations.razor.css`
- `BingoSim.Web/Components/Pages/Simulations/SimulationResults.razor`
- `BingoSim.Web/Components/Pages/Simulations/SimulationResults.razor.css`
- `BingoSim.Web/Services/SimulationRunQueueHostedService.cs`

**Tests**
- `Tests/BingoSim.Application.UnitTests/Simulation/RowUnlockHelperTests.cs`
- `Tests/BingoSim.Application.UnitTests/Simulation/RowRushAllocatorTests.cs`
- `Tests/BingoSim.Application.UnitTests/Simulation/GreedyPointsAllocatorTests.cs`
- `Tests/BingoSim.Application.UnitTests/Simulation/SeedDerivationTests.cs`
- `Tests/BingoSim.Application.UnitTests/Simulation/SimulationRunnerReproducibilityTests.cs`
- `Tests/BingoSim.Infrastructure.IntegrationTests/Repositories/SimulationBatchIntegrationTests.cs`

### Modified

- `BingoSim.Infrastructure/Persistence/AppDbContext.cs` — added DbSets for simulation entities
- `BingoSim.Infrastructure/DependencyInjection.cs` — registered simulation repositories, queue, snapshot builder, runner, executor, batch service
- `BingoSim.Web/Program.cs` — LocalSimulationOptions, SimulationRunQueueHostedService
- `BingoSim.Web/Components/Layout/MainLayout.razor` — nav link "Run Simulations"
- `BingoSim.Web/appsettings.json` — LocalSimulation section
- `BingoSim.Application/Services/DevSeedService.cs` — ITeamRepository, SeedTeamsAsync, reset order (teams before events)
- `Docs/DEV_SEEDING.md` — Slices 1–4, teams/strategy seeding, reset order
- `Tests/BingoSim.Application.UnitTests/Services/DevSeedServiceTests.cs` — added ITeamRepository to constructor

---

## How to Run Seeding After Update

**Idempotent seed (creates or updates; safe to run repeatedly):**
```bash
dotnet run --project BingoSim.Seed
```

**Reset and reseed (deletes only seed-tagged data, then re-seeds):**
```bash
dotnet run --project BingoSim.Seed -- --reset
```

Reset order: Teams (and StrategyConfigs + TeamPlayers) for seed events → Events → Activities → Players.

---

## How to Run a Local Batch from UI Using Seeded Data

1. Start PostgreSQL (e.g. `docker compose up -d postgres`).
2. Seed data: `dotnet run --project BingoSim.Seed` (or `-- --reset` for clean reseed).
3. Start Web: `dotnet run --project BingoSim.Web`.
4. Open `https://localhost:5001` (or the port in launchSettings).
5. Go to **Run Simulations**.
6. Select an event (e.g. "Winter Bingo 2025" or "Spring League Bingo"). Drafted teams (Team Alpha, Team Beta) and their strategies appear.
7. Set run count (e.g. 10 or 100), optionally enter a seed, leave execution mode **Local**.
8. Click **Start batch**. You are redirected to the Results page for that batch.
9. Progress updates (completed/failed/running/pending); when the batch completes, per-team aggregates and sample run timelines are shown.

---

## Dotnet Test Commands

**Full solution:**
```bash
dotnet test
```

**Application unit tests only:**
```bash
dotnet test Tests/BingoSim.Application.UnitTests/BingoSim.Application.UnitTests.csproj
```

**Infrastructure integration tests (Postgres Testcontainers):**
```bash
dotnet test Tests/BingoSim.Infrastructure.IntegrationTests/BingoSim.Infrastructure.IntegrationTests.csproj
```

**Core unit tests:**
```bash
dotnet test Tests/BingoSim.Core.UnitTests/BingoSim.Core.UnitTests.csproj
```

---

## Seed Storage (Adjustment)

- **SimulationBatch.Seed** and **SimulationRun.Seed** are persisted as **string** (not long).
- RNG seed is derived deterministically in Application from `(BatchSeedString + RunIndex)` via `SeedDerivation.DeriveRngSeed`; the run seed string for storage/UI is `SeedDerivation.DeriveRunSeedString(batchSeedString, runIndex)` (e.g. `"myseed_0"`).
