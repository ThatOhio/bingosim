# Slice 7: Distributed Workers — Review Summary

## Scope Delivered

- **Docker:** RabbitMQ container; Web and Worker depend on RabbitMQ; .env.example for RABBITMQ_HOST/PORT/USER/PASS
- **Message contracts:** ExecuteSimulationRun { SimulationRunId } in BingoSim.Shared (plain record)
- **Web:** Publishes ExecuteSimulationRun when starting distributed batch; returns immediately; RoutingSimulationRunWorkPublisher for retries
- **Worker:** Consumes ExecuteSimulationRun; atomic claim (Pending→Running); executes via Application; retries via re-publish (up to 5 attempts); BatchFinalizerHostedService for idempotent finalization
- **DB claim strategy:** TryClaimAsync conditional update on Status='Pending'; second claim fails when run already Running/Completed/Failed
- **Batch finalization:** BatchFinalizerHostedService; IBatchFinalizationService.TryFinalizeAsync; idempotent via TryTransitionToFinalAsync
- **Observability:** Structured logs (BatchId, RunId, Attempt); SimulationDelayMs throttle knob for parallelism validation

## Acceptance Alignment

| Doc Section | Status |
|-------------|--------|
| 5) Distributed workers | ✅ Web can close; workers continue; results visible |
| 5) Retries / terminal failure | ✅ AttemptCount; run Failed after 5; batch Error |
| 7) Results & Aggregations | ✅ Aggregates computed once runs terminal; idempotent |
| 8) Observability | ✅ Throttle knob; manual 2-worker vs 1-worker recipe |

## How to Run

- **Docker stack:** See SLICE7_COMPLETE.md / QUICKSTART.md
- **Distributed batch:** Run Simulations → select Distributed → Start batch → results visible on Simulation Results
- **Parallelism validation:** Set `WorkerSimulation__SimulationDelayMs=100` (or `WORKER_SIMULATION_DELAY_MS=100` in .env); see QUICKSTART for step-by-step guide
- **Tests:** `dotnet test`

## Files Changed (Summary)

- **Shared:** ExecuteSimulationRun
- **Core:** ISimulationRunRepository.TryClaimAsync; ISimulationBatchRepository.TryTransitionToFinalAsync
- **Application:** ISimulationRunWorkPublisher; IBatchFinalizationService; IUnfinalizedBatchesQuery; SimulationRunExecutor refactor; SimulationBatchService distributed branch
- **Infrastructure:** MassTransitRunWorkPublisher; RoutingSimulationRunWorkPublisher; BatchFinalizationService; BatchFinalizerHostedService; UnfinalizedBatchesQuery; TryClaimAsync impl; TryTransitionToFinalAsync impl; SimulationRunQueue implements ISimulationRunWorkPublisher
- **Web:** MassTransit; keyed ISimulationRunWorkPublisher; RabbitMQ config
- **Worker:** ExecuteSimulationRunConsumer; MassTransit; BatchFinalizerHostedService; Program rewrite
- **Tests:** SimulationRunRepositoryTryClaimTests; DistributedBatchIntegrationTests (success, terminal failure, idempotency); SimulationBatchServiceTests (distributedWorkPublisher)
