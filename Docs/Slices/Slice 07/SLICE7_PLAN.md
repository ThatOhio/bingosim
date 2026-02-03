# Slice 7: Distributed Workers (RabbitMQ + MassTransit) — Plan Only

**Scope:** Add distributed execution via RabbitMQ and MassTransit so that workers can process simulation runs independently. Web can be closed; workers continue. Retry/terminal failure behavior, observability, and parallelism validation per acceptance tests.

**Source of truth:** `Docs/06_Acceptance_Tests.md` — Sections 5 (Simulation Execution: distributed workers), 5 (Retries and terminal failure), 7 (Results & Aggregations), 8 (Observability & Performance Testability).

**Constraints:** Clean Architecture; simulation engine in Application; Worker connects directly to DB; messaging is fire-and-forget; identifier-only payloads; avoid ordering requirements.

---

## 1) Compose Changes (Services, Ports, Env Vars)

### 1.1 RabbitMQ Service

Add RabbitMQ container to `compose.yaml`:

```yaml
rabbitmq:
  image: rabbitmq:3-management-alpine
  container_name: bingosim-rabbitmq
  environment:
    RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER:-guest}
    RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASS:-guest}
  ports:
    - "5672:5672"   # AMQP
    - "15672:15672" # Management UI (dev)
  healthcheck:
    test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
    interval: 10s
    timeout: 5s
    retries: 5
```

### 1.2 Service Dependencies

- **bingosim.web**  
  - Add `depends_on: rabbitmq` with `condition: service_healthy`  
  - Add env vars (see below)

- **bingosim.worker**  
  - Add `depends_on: rabbitmq` with `condition: service_healthy`  
  - Add env vars (see below)

### 1.3 Environment Variables

Add `.env.example` and document in compose:

| Variable | Description | Default |
|----------|-------------|---------|
| `RABBITMQ_HOST` | RabbitMQ host | `rabbitmq` (compose) / `localhost` (dev) |
| `RABBITMQ_PORT` | AMQP port | `5672` |
| `RABBITMQ_USER` | AMQP user | `guest` |
| `RABBITMQ_PASS` | AMQP password | `guest` |
| `ConnectionStrings__DefaultConnection` | DB connection (existing) | — |

MassTransit will derive its connection string from these, e.g.:
`amqp://${RABBITMQ_USER}:${RABBITMQ_PASS}@${RABBITMQ_HOST}:${RABBITMQ_PORT}/`

### 1.4 Compose Summary

- **New service:** `rabbitmq` (5672, 15672)
- **Web/Worker:** Same DB connection; new RabbitMQ connection via env
- **Volumes:** Optional `rabbitmq_data` for persistence (recommended for dev)

---

## 2) MassTransit Configuration (Web + Worker)

### 2.1 Web (Publisher)

- Add `MassTransit` NuGet to `BingoSim.Web` (and `BingoSim.Infrastructure` if bus config lives there).
- Configure MassTransit with RabbitMQ:
  - Use env vars to build host/port/user/pass
  - Fire-and-forget: no request/response, no consumers in Web
- Register `AddMassTransit` with `UsingRabbitMq`; configure endpoint for `ExecuteSimulationRun` publish.
- Web publishes `ExecuteSimulationRun` when starting a distributed batch; no queue consumer.

### 2.2 Worker (Consumer)

- Add `MassTransit` NuGet to `BingoSim.Worker`.
- Configure MassTransit with RabbitMQ:
  - Same env-based connection
  - Add consumer for `ExecuteSimulationRun`
- Worker host uses `AddMassTransitHostedService` (or equivalent) to run the bus.
- No publish from Worker except for retry (re-publish same message).

### 2.3 Configuration Source

- Prefer `appsettings.json` + env override:
  - Section `RabbitMQ` or `MassTransit:RabbitMq` with `Host`, `Port`, `Username`, `Password`
  - Env vars override: `RABBITMQ_HOST`, etc.
- Connection string format: `amqp://user:pass@host:port/`

### 2.4 Retry Policy (MassTransit)

- MassTransit built-in retries: **disabled** for application-level retry (AttemptCount).
- Use `UseMessageRetry(r => r.None())` or equivalent so that consumer exceptions cause Nack and redelivery only if we re-publish; we handle retry via `AttemptCount` and explicit re-publish.

---

## 3) Message Contracts

### 3.1 Location

Contracts live in **`BingoSim.Shared`** (or `BingoSim.Application.Contracts` if a new project is preferred). `BingoSim.Shared` already exists and is empty; use it to avoid coupling Application to MassTransit.

### 3.2 Primary Message

```csharp
// BingoSim.Shared/Messages/ExecuteSimulationRun.cs
public record ExecuteSimulationRun
{
    public required Guid SimulationRunId { get; init; }
}
```

- Identifier-only; no snapshots or payloads over the bus.
- Used by Web (publish) and Worker (consume).

### 3.3 Optional: KickBatch

- **Not required** for Slice 7 if we publish one `ExecuteSimulationRun` per run.
- If preferred: `KickBatch { BatchId }` — one message; Worker expands to N run messages. Adds complexity; **recommend one message per run** for simplicity and parallelism.

### 3.4 Summary

- Single contract: `ExecuteSimulationRun { SimulationRunId }`
- Shared project references: `BingoSim.Core` (for nothing if no enums) or standalone. Keep Shared minimal: only the message record(s).

---

## 4) DB Claim Strategy (Exact Update Predicate)

### 4.1 Problem

Current flow: `run.MarkRunning()` then `runRepo.UpdateAsync(run)`. Two workers could both load a Pending run and both update to Running.

### 4.2 Atomic Claim

Add to `ISimulationRunRepository`:

```csharp
Task<bool> TryClaimAsync(Guid runId, DateTimeOffset startedAt, CancellationToken cancellationToken = default);
```

**Implementation (EF Core):**

```sql
UPDATE "SimulationRuns"
SET "Status" = 'Running', "StartedAt" = @startedAt, "LastAttemptAt" = @startedAt
WHERE "Id" = @runId AND "Status" = 'Pending'
```

- Execute via `ExecuteUpdateAsync` or raw SQL; return `rowsAffected > 0`.
- If `false`: another worker claimed it (or run is already terminal); consumer acks and skips.

### 4.3 Predicate Details

- **Exact predicate:** `Id = @runId AND Status = 'Pending'`
- Excludes: `Running`, `Completed`, `Failed`
- Concurrency guard: only one worker can transition Pending → Running.

### 4.4 Entity Method

- Keep `SimulationRun.MarkRunning(DateTimeOffset)` for in-memory state.
- Repository uses the predicate directly; no need to load entity for claim.
- After successful claim, load run for execution (or use a combined load+claim if desired; separate claim is simpler).

---

## 5) Worker Consumer Flow + Retry Handling

### 5.1 Consumer Flow

1. Receive `ExecuteSimulationRun { SimulationRunId }`
2. Load run from DB (or skip if not found)
3. If run is null → ack, skip
4. If run is terminal (`IsTerminal`) → ack, skip
5. **Atomic claim:** `TryClaimAsync(runId, startedAt)`  
   - If false → ack, skip
6. Load snapshot; if null → `FailRunAsync`, handle retry/terminal
7. Execute simulation (Application `SimulationRunner`)
8. Persist `TeamRunResult`, update run to Completed, call finalization
9. On exception:
   - `run.MarkFailed(message, attemptedAt)` (increments AttemptCount)
   - `runRepo.UpdateAsync(run)`
   - If terminal (AttemptCount >= 5): mark batch Error via finalization; ack
   - If not terminal: **re-publish** `ExecuteSimulationRun { run.Id }`; ack

### 5.2 Retry Handling

- **Transient failure:** Increment AttemptCount, re-publish if AttemptCount < 5.
- **Terminal failure:** AttemptCount >= 5; run marked Failed; batch marked Error via finalization.
- No MassTransit retry; all retries are application-controlled via re-publish.

### 5.3 Abstractions

- **ISimulationRunWorkPublisher** (or keep/extend existing): `ValueTask PublishRunWorkAsync(Guid runId, CancellationToken ct)`
  - Web (local): `SimulationRunQueue` implements it via `EnqueueAsync`
  - Worker (distributed): MassTransit implementation that publishes `ExecuteSimulationRun`
- `SimulationRunExecutor` depends on `ISimulationRunWorkPublisher` instead of (or in addition to) `ISimulationRunQueue` for retry. Rename/clarify: executor needs "enqueue run for (re)execution" — `ISimulationRunWorkPublisher` covers both.

### 5.4 SimulationRunExecutor Changes

- Replace `ISimulationRunQueue` with `ISimulationRunWorkPublisher` for retry path.
- For claim: executor currently does `run.MarkRunning` + `UpdateAsync`. In distributed mode, the **consumer** must do atomic claim before calling executor. Options:
  - **A)** Executor gets `TryClaimAsync` and does claim internally; if claim fails, return early (no-op).
  - **B)** Consumer does claim; if success, calls executor with "already claimed" run. Executor skips claim.
- **Recommend A:** Add `TryClaimAsync` to repository; executor calls it at start. If false, return. Unifies local and distributed behavior.

### 5.5 SimulationDelayMs in Worker

- Add `LocalSimulation` (or `Simulation`) section to Worker `appsettings.json` with `SimulationDelayMs`.
- Worker applies delay before/after execution for parallelism validation (manual test).
- Same knob as Web; Worker reads it for distributed runs.

---

## 6) Finalization Approach and Idempotency Strategy

### 6.1 Current Behavior

`SimulationRunExecutor.TryCompleteBatchAsync` runs after each run when all runs are terminal. It computes aggregates and marks batch Completed/Error. For distributed mode, multiple workers could call it concurrently.

### 6.2 Idempotency Requirements

- Finalization must be safe under concurrency.
- Only one process should compute and persist aggregates.
- Batch status transition must be atomic.

### 6.3 Approach: Dedicated BatchFinalizer + Idempotent Service

**Option A: BatchFinalizer Hosted Service (recommended)**

- New `BatchFinalizerHostedService` (can live in Web or Worker; prefer Worker to avoid Web responsibility).
- Periodically (e.g. every 10–30 seconds) scans batches where:
  - `Status IN ('Pending','Running')`
  - All runs for that batch are terminal (`Status IN ('Completed','Failed')`)
- For each such batch, call idempotent `IBatchFinalizationService.TryFinalizeAsync(batchId)`.

**IBatchFinalizationService (Application):**

- `Task<bool> TryFinalizeAsync(Guid batchId, CancellationToken ct)`
- Logic:
  1. Load batch; if null or already Completed/Error → return false
  2. Load runs; if any non-terminal → return false
  3. **Atomic status transition:** `UPDATE SimulationBatches SET Status = @newStatus WHERE Id = @id AND Status IN ('Pending','Running')` — use `Completed` or `Error` based on failed count
  4. If rows affected = 0 → return false (another process won)
  5. If rows affected = 1 → compute aggregates, persist, set CompletedAt/ErrorMessage; return true

**Extract logic from SimulationRunExecutor:**

- Move `TryCompleteBatchAsync` implementation into `IBatchFinalizationService`.
- `SimulationRunExecutor` calls `IBatchFinalizationService.TryFinalizeAsync(batchId)` instead of inline logic (for local mode, immediate finalization).
- `BatchFinalizerHostedService` calls same service (for distributed, eventual finalization).

### 6.4 Idempotency Strategy

- **Batch status update:** Use conditional update: `WHERE Id = @id AND Status IN ('Pending','Running')`. Only one concurrent call succeeds.
- **Aggregates:** Compute only after winning the status update. No double-write.
- **CompletedAt / ErrorMessage:** Set in same transaction as status update.

### 6.5 Worker vs Web for BatchFinalizer

- **Worker:** Keeps finalization close to run execution; no Web dependency.
- **Web:** Could also host BatchFinalizer if Web stays running; but acceptance says "Web can be closed" — so **Worker** is the correct host for BatchFinalizer.

---

## 7) Test Plan (Unit + Integration + Manual Validation)

### 7.1 Unit Tests

**Application**

- `IBatchFinalizationService` / implementation: mock repos; verify conditional update logic and aggregate computation when applicable.
- `SimulationRunExecutor` with `TryClaimAsync` returning false: verify early return, no execution.
- Retry path: mock `ISimulationRunWorkPublisher`; verify re-publish when AttemptCount < 5, no re-publish when terminal.

**Infrastructure**

- `SimulationRunRepository.TryClaimAsync`: verify SQL/EF behavior (unit with in-memory or integration with real DB).
- MassTransit publisher wrapper: verify `PublishRunWorkAsync` publishes correct message (unit with test harness).

**Worker**

- Consumer: given `ExecuteSimulationRun`, mock executor and repos; verify claim, execution, retry publish, terminal handling.

### 7.2 Integration Tests

- **Worker + DB + RabbitMQ (optional):** Full flow: publish message, worker consumes, executes, persists. Requires testcontainers or similar for RabbitMQ + Postgres.
- **BatchFinalizer:** Given batch with all runs terminal, run finalizer; verify batch Completed and aggregates present. Run twice; verify idempotent (no duplicate aggregates, no error).

### 7.3 Manual Validation (Recipe)

**Parallelism validation (per acceptance 8):**

1. Set `SimulationDelayMs` (e.g. 100) in Worker `appsettings.json` or env.
2. Start 1 Worker; run batch of e.g. 20 runs; measure elapsed time T1.
3. Start 2 Workers; run batch of 20 runs; measure elapsed time T2.
4. Expect T2 < T1 (measurable improvement; no brittle timing assertions).
5. Document in QUICKSTART or REVIEW.

**Distributed execution (per acceptance 5):**

1. Start compose (Postgres, RabbitMQ, Web, 1+ Worker).
2. Start batch in distributed mode (1000 runs).
3. Stop Web container; verify workers continue and batch completes.
4. Restart Web; verify results visible.

**Retry / terminal failure:**

1. Simulate transient failure (e.g. inject exception for first N attempts in test).
2. Verify AttemptCount increments, run eventually Completes or Fails.
3. Verify batch Error when run fails after 5 attempts.

### 7.4 Avoid Brittle Timing

- No assertions on exact run/sec or absolute duration.
- Only relative: 2 workers faster than 1 worker.

---

## 8) Exact File List to Create/Modify

### Create

**BingoSim.Shared**

- `BingoSim.Shared/Messages/ExecuteSimulationRun.cs`

**BingoSim.Application**

- `BingoSim.Application/Interfaces/IBatchFinalizationService.cs`
- `BingoSim.Application/Interfaces/ISimulationRunWorkPublisher.cs` (or extend existing)
- `BingoSim.Application/Services/BatchFinalizationService.cs` (if logic lives in Application) or in Infrastructure

**BingoSim.Core**

- (Optional) No new files; `TryClaimAsync` extends existing `ISimulationRunRepository`

**BingoSim.Infrastructure**

- `BingoSim.Infrastructure/Simulation/MassTransitRunWorkPublisher.cs` (implements `ISimulationRunWorkPublisher` via MassTransit)
- `BingoSim.Infrastructure/Services/BatchFinalizationService.cs` (implements `IBatchFinalizationService`, uses EF/repos)
- `BingoSim.Infrastructure/Hosting/BatchFinalizerHostedService.cs` (periodic scan + finalization)

**BingoSim.Worker**

- `BingoSim.Worker/Consumers/ExecuteSimulationRunConsumer.cs`
- `BingoSim.Worker/Program.cs` (rewrite: host with MassTransit, DI, DbContext, migrations)

**BingoSim.Web**

- (No new files; modifications only)

**Config / Docker**

- `.env.example` (RabbitMQ vars)
- (Optional) `compose.override.yaml` for local overrides

**Tests**

- `Tests/BingoSim.Application.UnitTests/Services/BatchFinalizationServiceTests.cs`
- `Tests/BingoSim.Application.UnitTests/Services/SimulationRunExecutorDistributedTests.cs` (or extend existing)
- `Tests/BingoSim.Infrastructure.IntegrationTests/Repositories/SimulationRunRepositoryTryClaimTests.cs`
- `Tests/BingoSim.Worker.UnitTests/Consumers/ExecuteSimulationRunConsumerTests.cs`
- (Optional) `Tests/BingoSim.Infrastructure.IntegrationTests/MassTransitPublisherIntegrationTests.cs`

**Docs**

- `Docs/Slice 07/QUICKSTART.md` (manual validation recipe)
- `Docs/Slice 07/SLICE7_PLAN.md` (this file)

### Modify

**compose.yaml**

- Add `rabbitmq` service
- Add `depends_on` and env vars for `bingosim.web` and `bingosim.worker`

**BingoSim.Core**

- `BingoSim.Core/Interfaces/ISimulationRunRepository.cs` — add `TryClaimAsync`

**BingoSim.Application**

- `BingoSim.Application/Interfaces/ISimulationRunExecutor.cs` — (optional signature change if needed)
- `BingoSim.Application/Services/SimulationRunExecutor.cs` — use `TryClaimAsync`, `ISimulationRunWorkPublisher`, `IBatchFinalizationService`
- `BingoSim.Application/Interfaces/ISimulationRunQueue.cs` — either keep for local Dequeue or introduce `ISimulationRunWorkPublisher` and have Queue implement it for Enqueue

**BingoSim.Infrastructure**

- `BingoSim.Infrastructure/Persistence/Repositories/SimulationRunRepository.cs` — implement `TryClaimAsync`
- `BingoSim.Infrastructure/DependencyInjection.cs` — register `IBatchFinalizationService`, `ISimulationRunWorkPublisher` (MassTransit impl for Worker; Queue impl for Web), `BatchFinalizerHostedService` (Worker only)

**BingoSim.Web**

- `BingoSim.Web/Program.cs` — add MassTransit, configure publish; when `ExecutionMode.Distributed`, publish `ExecuteSimulationRun` per run instead of enqueue
- `BingoSim.Web/appsettings.json` — add RabbitMQ / MassTransit section
- `BingoSim.Application/Services/SimulationBatchService.cs` — for Distributed mode: set batch Running, publish one `ExecuteSimulationRun` per run, return immediately

**BingoSim.Worker**

- `BingoSim.Worker/Program.cs` — host setup: DbContext, repos, Application services, MassTransit consumer, BatchFinalizerHostedService
- `BingoSim.Worker/BingoSim.Worker.csproj` — add MassTransit, BingoSim.Infrastructure (or existing refs)
- `BingoSim.Worker/appsettings.json` — add ConnectionStrings, RabbitMQ, SimulationDelayMs

**BingoSim.Shared**

- `BingoSim.Shared/BingoSim.Shared.csproj` — add MassTransit.Contracts or ensure message can be used by MassTransit (likely no extra deps if record is simple)

**BingoSim.Web Components**

- `BingoSim.Web/Components/Pages/Simulations/RunSimulations.razor` — change "Distributed (coming soon)" to "Distributed" (remove stub label)

### Files Summary

| Action | Path |
|--------|------|
| Create | `Docs/Slice 07/SLICE7_PLAN.md` |
| Create | `BingoSim.Shared/Messages/ExecuteSimulationRun.cs` |
| Create | `BingoSim.Application/Interfaces/IBatchFinalizationService.cs` |
| Create | `BingoSim.Application/Interfaces/ISimulationRunWorkPublisher.cs` |
| Create | `BingoSim.Application/Services/BatchFinalizationService.cs` (or Infrastructure) |
| Create | `BingoSim.Infrastructure/Simulation/MassTransitRunWorkPublisher.cs` |
| Create | `BingoSim.Infrastructure/Hosting/BatchFinalizerHostedService.cs` |
| Create | `BingoSim.Worker/Consumers/ExecuteSimulationRunConsumer.cs` |
| Create | `.env.example` |
| Modify | `compose.yaml` |
| Modify | `BingoSim.Core/Interfaces/ISimulationRunRepository.cs` |
| Modify | `BingoSim.Infrastructure/Persistence/Repositories/SimulationRunRepository.cs` |
| Modify | `BingoSim.Application/Services/SimulationRunExecutor.cs` |
| Modify | `BingoSim.Application/Services/SimulationBatchService.cs` |
| Modify | `BingoSim.Infrastructure/DependencyInjection.cs` |
| Modify | `BingoSim.Web/Program.cs` |
| Modify | `BingoSim.Worker/Program.cs` |
| Modify | `BingoSim.Web/appsettings.json` |
| Modify | `BingoSim.Worker/appsettings.json` |
| Modify | `BingoSim.Web/Components/Pages/Simulations/RunSimulations.razor` |
| Modify | csproj files (MassTransit, Shared refs) |
| Create | Test files per section 7 |

---

## Summary

- **Docker:** RabbitMQ + management UI; env vars for host/port/user/pass.
- **Contracts:** `ExecuteSimulationRun { SimulationRunId }` in BingoSim.Shared.
- **Web:** Publishes one message per run when starting distributed batch; returns immediately.
- **Worker:** Consumes messages; atomic claim (Pending→Running); executes via Application; retries via re-publish up to 5 attempts; terminal failure marks batch Error.
- **Finalization:** Dedicated BatchFinalizer hosted service in Worker; idempotent via conditional batch status update.
- **Observability:** Structured logs (BatchId, RunId, Attempt); SimulationDelayMs for parallelism validation.
- **Tests:** Unit for claim, finalization, retry; integration for full flow; manual recipe for 2 workers vs 1.

No code is written in this plan; implementation follows in a subsequent step.
