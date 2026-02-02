# Acceptance Test Review - Distributed Workers (Slice 7)

## Review Date
2026-02-02

## Source
`Docs/06_Acceptance_Tests.md` — Section "5) Simulation Execution" (distributed workers, retries/terminal failure), Section "7) Results & Aggregations", Section "8) Observability & Performance Testability"

---

## Acceptance Criteria Verification

### ✅ Section 5: Simulation Execution — Distributed Workers

- [x] **Run using distributed workers:** Workers execute and persist results; Web UI can be closed without stopping execution; later, UI shows completed batch results
- [x] **Retries and terminal failure:** Attempt count incremented and stored; after 5 attempts: run marked Failed, batch marked Error; UI shows batch errored and why

### ✅ Section 5: Simulation Execution — Local Execution

- [x] **Run using local execution:** Batch completes without requiring external worker containers; Results persisted and viewable (unchanged)

### ✅ Section 7: Results & Aggregations

- [x] Batch-level aggregates stored and displayed (via BatchFinalizer; idempotent)
- [x] UI uses stored aggregates; no recomputation

### ✅ Section 8: Observability & Performance Testability

- [x] **Metrics:** Runs completed/failed/retried; estimated runs/sec; batch duration (existing InMemorySimulationMetrics)
- [x] **Multi-worker parallelism validation:** SimulationDelayMs throttle knob; two workers outperform one worker on same batch size (manual recipe in QUICKSTART)

---

## Implementation Summary

| Area | Implementation |
|------|----------------|
| **Docker** | RabbitMQ container (5672, 15672); Web and Worker depend on RabbitMQ; .env.example for RABBITMQ_* |
| **Shared** | ExecuteSimulationRun { SimulationRunId } — plain record, no MassTransit attributes |
| **Application** | ISimulationRunWorkPublisher (Guid-based); IBatchFinalizationService; ISimulationRunExecutor uses TryClaimAsync, work publisher, finalization service |
| **Infrastructure** | MassTransitRunWorkPublisher; RoutingSimulationRunWorkPublisher (Web); BatchFinalizationService; BatchFinalizerHostedService; TryClaimAsync on SimulationRunRepository; TryTransitionToFinalAsync on SimulationBatchRepository |
| **Web** | MassTransit publisher; distributed branch in SimulationBatchService; keyed "distributed" ISimulationRunWorkPublisher |
| **Worker** | ExecuteSimulationRunConsumer; MassTransit host; BatchFinalizerHostedService; SimulationDelayMs for parallelism validation |
| **Tests** | SimulationRunRepositoryTryClaimTests (concurrency: Pending→true, Running/Completed/Failed→false); DistributedBatchIntegrationTests (5 runs, MassTransit test harness, aggregates) |

---

## Test Coverage

- **Unit (Application):** SimulationBatchServiceTests — distributedWorkPublisher parameter; SimulationRunExecutor uses TryClaimAsync, work publisher, finalization (covered by integration)
- **Integration (Infrastructure):** SimulationRunRepositoryTryClaimTests — TryClaimAsync_PendingRun_ReturnsTrue; TryClaimAsync_AlreadyRunning_ReturnsFalse; TryClaimAsync_AlreadyCompleted_ReturnsFalse; TryClaimAsync_AlreadyFailed_ReturnsFalse; DistributedBatchIntegrationTests — DistributedBatch_FiveRuns_CompletesWithAggregates; TerminalFailure_AllRunsTerminalWithOneFailed_MarksBatchErrorWithErrorMessage; BatchFinalization_SecondCall_ReturnsFalseIdempotent

---

## Post-Review Notes

- **Message contracts:** Plain records in BingoSim.Shared; no MassTransit references; Application depends only on ISimulationRunWorkPublisher (Guid-based)
- **Local mode intact:** Web continues to use SimulationRunQueue for local execution; RoutingSimulationRunWorkPublisher routes retries to queue (local) or MassTransit (distributed) based on batch mode
- **No ordering assumptions:** Workers execute runs in any order; fire-and-forget messaging
