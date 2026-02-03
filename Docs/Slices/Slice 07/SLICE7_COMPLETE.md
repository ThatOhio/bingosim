# Slice 7: Distributed Workers (RabbitMQ + MassTransit) — Complete

## Delivered Scope

- **Docker:** RabbitMQ container; Web and Worker depend on RabbitMQ; .env.example
- **Message contracts:** ExecuteSimulationRun { SimulationRunId } in BingoSim.Shared (plain record, no MassTransit attributes)
- **Web:** Publishes ExecuteSimulationRun when starting distributed batch; returns immediately; local mode intact
- **Worker:** Consumes ExecuteSimulationRun; atomic claim (TryClaimAsync); executes via Application; retries via re-publish (up to 5); BatchFinalizerHostedService
- **Batch finalization:** Idempotent via TryTransitionToFinalAsync; UnfinalizedBatchesQuery; BatchFinalizerHostedService
- **Observability:** Structured logs; SimulationDelayMs throttle knob

---

## File List Changed

### Created

**BingoSim.Shared**
- `BingoSim.Shared/Messages/ExecuteSimulationRun.cs`

**BingoSim.Application**
- `BingoSim.Application/Interfaces/ISimulationRunWorkPublisher.cs`
- `BingoSim.Application/Interfaces/IBatchFinalizationService.cs`
- `BingoSim.Application/Interfaces/IUnfinalizedBatchesQuery.cs`

**BingoSim.Infrastructure**
- `BingoSim.Infrastructure/Simulation/MassTransitRunWorkPublisher.cs`
- `BingoSim.Infrastructure/Simulation/RoutingSimulationRunWorkPublisher.cs`
- `BingoSim.Infrastructure/Services/BatchFinalizationService.cs`
- `BingoSim.Infrastructure/Hosting/BatchFinalizerHostedService.cs`
- `BingoSim.Infrastructure/Queries/UnfinalizedBatchesQuery.cs`

**BingoSim.Worker**
- `BingoSim.Worker/Consumers/ExecuteSimulationRunConsumer.cs`
- `BingoSim.Worker/appsettings.json`

**Config**
- `.env.example`

**Tests**
- `Tests/BingoSim.Infrastructure.IntegrationTests/Repositories/SimulationRunRepositoryTryClaimTests.cs`
- `Tests/BingoSim.Infrastructure.IntegrationTests/Simulation/DistributedBatchIntegrationTests.cs` — DistributedBatch_FiveRuns_CompletesWithAggregates; TerminalFailure_AllRunsTerminalWithOneFailed_MarksBatchErrorWithErrorMessage; BatchFinalization_SecondCall_ReturnsFalseIdempotent

**Docs**
- `Docs/Slice 07/ACCEPTANCE_REVIEW.md`
- `Docs/Slice 07/REVIEW_SUMMARY.md`
- `Docs/Slice 07/QUICKSTART.md`
- `Docs/Slice 07/SLICE7_COMPLETE.md`

### Modified

**compose.yaml**
- Added rabbitmq service; volumes; depends_on and env vars for Web and Worker
- WorkerSimulation__SimulationDelayMs (default 0) for parallelism validation

**BingoSim.Core**
- `BingoSim.Core/Interfaces/ISimulationRunRepository.cs` — TryClaimAsync
- `BingoSim.Core/Interfaces/ISimulationBatchRepository.cs` — TryTransitionToFinalAsync

**BingoSim.Application**
- `BingoSim.Application/Services/SimulationRunExecutor.cs` — TryClaimAsync, ISimulationRunWorkPublisher, IBatchFinalizationService
- `BingoSim.Application/Services/SimulationBatchService.cs` — distributed branch; [FromKeyedServices("distributed")] ISimulationRunWorkPublisher

**BingoSim.Infrastructure**
- `BingoSim.Infrastructure/Persistence/Repositories/SimulationRunRepository.cs` — TryClaimAsync
- `BingoSim.Infrastructure/Persistence/Repositories/SimulationBatchRepository.cs` — TryTransitionToFinalAsync
- `BingoSim.Infrastructure/Simulation/SimulationRunQueue.cs` — implements ISimulationRunWorkPublisher
- `BingoSim.Infrastructure/DependencyInjection.cs` — IBatchFinalizationService, IUnfinalizedBatchesQuery, SimulationRunQueue/MassTransitRunWorkPublisher

**BingoSim.Web**
- `BingoSim.Web/Program.cs` — MassTransit; keyed ISimulationRunWorkPublisher; RoutingSimulationRunWorkPublisher
- `BingoSim.Web/appsettings.json` — RabbitMQ section
- `BingoSim.Web/Components/Pages/Simulations/RunSimulations.razor` — "Distributed" (removed "coming soon")
- `BingoSim.Web/Components/Pages/Simulations/Batches.razor` — auto-refresh every 5s when batches Running/Pending

**BingoSim.Worker**
- `BingoSim.Worker/Program.cs` — MassTransit consumer; BatchFinalizerHostedService; DI setup

**csproj**
- BingoSim.Infrastructure, BingoSim.Web, BingoSim.Worker — MassTransit.RabbitMQ
- BingoSim.Application — Microsoft.Extensions.DependencyInjection.Abstractions
- BingoSim.Worker — Microsoft.Extensions.Hosting, Microsoft.Extensions.DependencyInjection
- Tests/BingoSim.Infrastructure.IntegrationTests — MassTransit.TestFramework; BingoSim.Worker reference

**Tests**
- `Tests/BingoSim.Application.UnitTests/Services/SimulationBatchServiceTests.cs` — distributedWorkPublisher parameter

---

## Docker Compose Instructions

### Start RabbitMQ + Web + N workers

```bash
# All services
docker compose up -d

# Or step by step
docker compose up -d postgres rabbitmq
docker compose up -d bingosim.web
docker compose up -d --scale bingosim.worker=2   # 2 workers
```

### Run a distributed batch from the UI

1. Open Web UI (e.g. http://localhost:8080)
2. Go to Run Simulations
3. Select Event, teams; set Run count (e.g. 100)
4. Select **Distributed** mode
5. Click "Start batch"
6. Navigate to Simulation Results → Batches (list auto-refreshes every 5s while batches run) → click batch to view progress/results
7. Optional: stop Web; workers continue; restart Web to see results

---

## dotnet test commands

```bash
# Full solution
dotnet test

# Unit tests only (faster)
dotnet test Tests/BingoSim.Application.UnitTests/BingoSim.Application.UnitTests.csproj
dotnet test Tests/BingoSim.Core.UnitTests/BingoSim.Core.UnitTests.csproj

# Integration tests (use Testcontainers Postgres; may take longer)
dotnet test Tests/BingoSim.Infrastructure.IntegrationTests/BingoSim.Infrastructure.IntegrationTests.csproj
```

---

## Migration Notes

**None.** No new migrations. TryClaimAsync and TryTransitionToFinalAsync use existing schema.
