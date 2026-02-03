# Batch Publish Refactor — Plan and Feedback

**Date:** February 3, 2025  
**Status:** Implemented — see [Batch_Publish_Refactor_Implementation.md](Batch_Publish_Refactor_Implementation.md)  
**Context:** `SimulationBatchService.StartBatchAsync` enqueues/publishes N runs one-at-a-time. For 10,000 runs, this is inefficient.

---

## 1. Current Behavior

### 1.1 Flow Summary

When a user starts a batch with RunCount = 10,000:

1. `SimulationBatchService.StartBatchAsync` creates the batch, snapshot, and 10,000 `SimulationRun` entities.
2. Runs are persisted via `runRepo.AddRangeAsync(runs)` — **already batched**.
3. A background `Task.Run` enqueues or publishes each run ID:

```csharp
// Lines 109–133 of SimulationBatchService.cs
var runIds = runs.Select(r => r.Id).ToList();
if (mode == ExecutionMode.Local)
{
    foreach (var runId in runIds)
        await runQueue.EnqueueAsync(runId);
}
else
{
    var publisher = scope.ServiceProvider.GetRequiredKeyedService<ISimulationRunWorkPublisher>("distributed");
    foreach (var runId in runIds)
        await publisher.PublishRunWorkAsync(runId);
}
```

### 1.2 Sequential Await Problem

Both branches use **sequential `await`** in a loop:

- **Local mode:** 10,000 sequential `await runQueue.EnqueueAsync(runId)` — each writes to a bounded `Channel<Guid>`.
- **Distributed mode:** 10,000 sequential `await publisher.PublishRunWorkAsync(runId)` — each does `await publishEndpoint.Publish(new ExecuteSimulationRun { ... })`, i.e. a network round-trip to RabbitMQ.

### 1.3 Impact

| Mode        | Operation                    | 10K runs ≈ | Bottleneck              |
|-------------|------------------------------|------------|--------------------------|
| Local       | `Channel.Writer.WriteAsync`  | ~10K awaits| In-memory, but sequential|
| Distributed | `IPublishEndpoint.Publish`   | ~10K awaits| **Network round-trips**  |

For distributed mode, 10,000 sequential publishes mean 10,000 serialized network calls. At ~1–5 ms per round-trip, that can add **10–50+ seconds** before all work is even queued, during which the HTTP response has already returned but workers sit idle.

---

## 2. Investigation Findings

### 2.1 MassTransit Batch Support

MassTransit provides `PublishBatch(IEnumerable<T>)` on `IPublishEndpoint`:

- Uses `Task.WhenAll` internally — publishes run **in parallel**, not sequentially.
- Each message still becomes its own message on the broker.
- The transport **may** batch messages into a single broker API call (transport-dependent).
- Even without transport batching, parallelizing removes the sequential bottleneck.

**Source:** [MassTransit Producers — Publish a batch of messages](https://masstransit.massient.com/concepts/producers)

### 2.2 Current Interface

```csharp
// ISimulationRunWorkPublisher
ValueTask PublishRunWorkAsync(Guid runId, CancellationToken cancellationToken = default);
```

Single-run only. No batch overload.

### 2.3 Local Mode (Channel)

`SimulationRunQueue` uses `Channel<Guid>` with capacity 100,000. Each `EnqueueAsync` is a `WriteAsync`. There is no built-in batch write, but we can:

- Use `Task.WhenAll(runIds.Select(id => EnqueueAsync(id)))` to parallelize.
- Or add `EnqueueRangeAsync(IEnumerable<Guid>)` that does the same internally.

### 2.4 Other Call Sites

| Location                         | Usage                                      | Would benefit from batch? |
|----------------------------------|--------------------------------------------|---------------------------|
| `SimulationBatchService`         | Start batch (local + distributed)          | **Yes**                   |
| `SimulationRunExecutor`         | Retry single run (1 message)               | No                        |
| `BingoSim.Seed` — `--recover-batch` | Re-publish stuck runs one-by-one        | **Yes** (smaller batches) |

---

## 3. Recommended Approach

### 3.1 Add Batch Method to Interface

Introduce a batch overload that accepts multiple run IDs:

```csharp
// ISimulationRunWorkPublisher (proposed addition)
ValueTask PublishRunWorkBatchAsync(IReadOnlyList<Guid> runIds, CancellationToken cancellationToken = default);
```

Keep `PublishRunWorkAsync(Guid)` for single-run retries and other one-off cases.

### 3.2 Implementation Strategy

| Implementation              | Batch behavior                                                                 |
|-----------------------------|----------------------------------------------------------------------------------|
| `MassTransitRunWorkPublisher` | `await publishEndpoint.PublishBatch(runIds.Select(id => new ExecuteSimulationRun { SimulationRunId = id }))` |
| `SimulationRunQueue`       | `await Task.WhenAll(runIds.Select(id => EnqueueAsync(id, cancellationToken)))`  |
| `RoutingSimulationRunWorkPublisher` | Route by batch mode; call queue or distributed batch method              |

### 3.3 Caller Changes

**SimulationBatchService** — replace the loop with a single batch call:

```csharp
if (mode == ExecutionMode.Local)
    await runQueue.EnqueueBatchAsync(runIds, cancellationToken);  // or use publisher
else
    await publisher.PublishRunWorkBatchAsync(runIds, cancellationToken);
```

**Note:** Local mode currently uses `runQueue` directly, not the publisher. Two options:

- **A)** Add `EnqueueBatchAsync` to `ISimulationRunQueue` and keep using the queue for local mode.
- **B)** Use a unified publisher for both modes and have it route to queue vs MassTransit; the publisher’s batch method handles both.

Option **B** keeps the service simpler (one code path) and aligns with the existing `RoutingSimulationRunWorkPublisher` pattern.

### 3.4 Chunking for Very Large Batches

For 100K+ runs, firing 100K parallel `Publish` or `Enqueue` tasks could cause memory or connection pressure. Consider:

- Chunking (e.g. 1,000–2,000 run IDs per batch).
- `PublishRunWorkBatchAsync` could chunk internally, or callers could chunk before calling.

For 10K runs, a single batch is likely fine; chunking can be added later if needed.

---

## 4. Implementation Plan (When Ready)

### 4.1 Phase 1 — Interface and Implementations

| Step | Task                                                                 | Files |
|------|----------------------------------------------------------------------|-------|
| 1    | Add `PublishRunWorkBatchAsync(IReadOnlyList<Guid>, ct)` to `ISimulationRunWorkPublisher` | `BingoSim.Application/Interfaces/ISimulationRunWorkPublisher.cs` |
| 2    | Implement batch in `MassTransitRunWorkPublisher` via `PublishBatch`  | `BingoSim.Infrastructure/Simulation/MassTransitRunWorkPublisher.cs` |
| 3    | Add `EnqueueBatchAsync` to `ISimulationRunQueue` and `SimulationRunQueue` | `BingoSim.Application/Interfaces/ISimulationRunQueue.cs`, `SimulationRunQueue.cs` |
| 4    | Implement batch in `SimulationRunQueue` (or `PublishRunWorkBatchAsync` that calls `EnqueueBatchAsync`) | `SimulationRunQueue.cs` |
| 5    | Implement `PublishRunWorkBatchAsync` in `RoutingSimulationRunWorkPublisher` | `RoutingSimulationRunWorkPublisher.cs` |

### 4.2 Phase 2 — Callers

| Step | Task                                                                 | Files |
|------|----------------------------------------------------------------------|-------|
| 6    | Use batch method in `SimulationBatchService.StartBatchAsync`          | `BingoSim.Application/Services/SimulationBatchService.cs` |
| 7    | Use batch method in Seed `--recover-batch`                            | `BingoSim.Seed/Program.cs` |

### 4.3 Phase 3 — Tests

| Step | Task                                                                 | Files |
|------|----------------------------------------------------------------------|-------|
| 8    | Unit tests for `PublishRunWorkBatchAsync` (mock publisher, verify batch call) | `SimulationBatchServiceTests.cs` |
| 9    | Integration test: batch publish → workers consume all                 | `DistributedBatchIntegrationTests.cs` |
| 10   | Unit test for `SimulationRunQueue.EnqueueBatchAsync` (or batch publisher path) | New or existing queue tests |

### 4.4 Optional — Chunking

If needed for 50K+ runs:

- Add config: `BatchPublishChunkSize` (default 2000).
- In `MassTransitRunWorkPublisher.PublishRunWorkBatchAsync`, chunk `runIds` and `await Task.WhenAll(chunks.Select(c => PublishBatch(c)))`.

---

## 5. Expected Impact

| Scenario              | Before (approx.)     | After (approx.)      |
|-----------------------|----------------------|------------------------|
| 10K distributed publish | 10–50+ s sequential | ~1–5 s parallel/batch |
| 10K local enqueue     | 10K sequential awaits| Parallel; sub-second  |
| Recover 100 stuck runs| 100 sequential       | 1 batch call          |

---

## 6. Risks and Mitigations

| Risk                          | Mitigation                                                                 |
|-------------------------------|----------------------------------------------------------------------------|
| Memory pressure with huge batches | Chunking; configurable chunk size                                      |
| Breaking existing consumers   | No change to message format; each run still one `ExecuteSimulationRun`     |
| Retry path regression        | Keep `PublishRunWorkAsync(Guid)`; retries stay single-message              |

---

## 7. Out of Scope (This Refactor)

- Changing message contract (still one message per run).
- Batch consumers (`IConsumer<Batch<ExecuteSimulationRun>>`) — would require worker changes.
- Transport-level `ConfigureBatchPublish` (deprecated in MassTransit v8).
- Single “batch” message containing all run IDs (different architecture).

---

## 8. References

- [MassTransit Producers — Publish a batch of messages](https://masstransit.massient.com/concepts/producers)
- [MassTransit Discussion #5839 — PublishBatch vs Publish network savings](https://github.com/MassTransit/MassTransit/discussions/5839)
- `SimulationBatchService.cs` lines 103–133
- `MassTransitRunWorkPublisher.cs`
- `SimulationRunQueue.cs`
- `Docs/PERF_NOTES.md`
- `Docs/Perf Testing/Round 2/Perf_Round2_Optimization_Plan.md`
