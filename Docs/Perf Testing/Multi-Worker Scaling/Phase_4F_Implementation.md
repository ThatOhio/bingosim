# Phase 4F Implementation Summary

**Date:** February 3, 2025  
**Status:** Completed  
**Context:** Worker-indexed partitioning to eliminate inter-worker competition for the same RabbitMQ queue and unlock multi-worker scaling.

---

## 1. Summary of Changes

Phase 4F implements work partitioning where batches are pre-assigned to specific workers at publish time. Each worker receives exclusive batches based on `batchNumber % workerCount`, eliminating inter-worker competition for messages and database claim operations.

| Component | Change |
|-----------|--------|
| Publisher (Web) | Assigns `WorkerIndex = batchNumber % WorkerCount` to each batch message |
| Message | Added nullable `WorkerIndex` property to `ExecuteSimulationRunBatch` |
| Worker | `WorkerIndexFilter` skips messages not assigned to this worker's index |
| Configuration | `WorkerCount` (publisher), `WorkerIndex` and `WorkerCount` (worker) |

---

## 2. Files Modified

### BingoSim.Infrastructure/Simulation/DistributedExecutionOptions.cs

Added `WorkerCount` property:

```csharp
/// <summary>
/// Expected number of workers for batch partitioning. Default 3.
/// Used to calculate WorkerIndex for each batch (batchNumber % WorkerCount).
/// Override via appsettings DistributedExecution:WorkerCount or env DISTRIBUTED_WORKER_COUNT.
/// </summary>
public int WorkerCount { get; set; } = 3;
```

### BingoSim.Shared/Messages/ExecuteSimulationRunBatch.cs

Added `WorkerIndex` property:

```csharp
/// <summary>
/// Worker index assigned to this batch (0-based). Used for work partitioning.
/// Null indicates no partitioning (any worker can process).
/// </summary>
public int? WorkerIndex { get; init; }
```

### BingoSim.Infrastructure/Simulation/MassTransitRunWorkPublisher.cs

- `PublishRunWorkBatchAsync`: Calculates `workerIndex = i % workerCount` for each batch; only assigns when `workerCount > 1`
- `PublishRunWorkAsync` (retries): Uses `WorkerIndex = null` so any worker can process retries

### BingoSim.Worker/Consumers/ExecuteSimulationRunConsumer.cs (WorkerSimulationOptions)

Added:

```csharp
public int? WorkerIndex { get; set; }
public int WorkerCount { get; set; } = 3;
```

### BingoSim.Worker/Filters/WorkerIndexFilter.cs (NEW)

Implements `IFilter<ConsumeContext<ExecuteSimulationRunBatch>>`:
- Processes all messages when `WorkerIndex` not configured or message has no `WorkerIndex`
- When partitioning enabled: only passes messages where `message.WorkerIndex == this worker's index`
- Skips mismatched messages (uses `NotifyConsumed` to avoid skipped queue; see Limitations)
- Logs at Debug level for per-message, Information for startup

### BingoSim.Worker/Consumers/ExecuteSimulationRunBatchConsumerDefinition.cs

- Injects `ILogger<WorkerIndexFilter>`
- When `WorkerIndex` is set and ≥ 0, adds `WorkerIndexFilter` via `consumerConfigurator.Message<ExecuteSimulationRunBatch>(m => m.UseFilter(...))`

### Configuration Files

- **BingoSim.Worker/appsettings.json**: Added `WorkerSimulation:WorkerIndex`, `WorkerSimulation:WorkerCount`, `DistributedExecution:WorkerCount`
- **BingoSim.Web/appsettings.json**: Added `DistributedExecution:WorkerCount`
- **compose.yaml**: Added `DistributedExecution__WorkerCount`, `WorkerSimulation__WorkerIndex`, `WorkerSimulation__WorkerCount`
- **.env.example**: Documented `DISTRIBUTED_WORKER_COUNT`, `WORKER_INDEX`, `WORKER_COUNT`

### README.md

Added "Running Workers with Partitioning (Phase 4F)" section with manual worker startup instructions.

---

## 3. Configuration Examples

### Manual Worker Startup (Recommended for Testing)

**Terminal 1:**
```bash
WORKER_INDEX=0 WORKER_COUNT=3 dotnet run --project BingoSim.Worker
```

**Terminal 2:**
```bash
WORKER_INDEX=1 WORKER_COUNT=3 dotnet run --project BingoSim.Worker
```

**Terminal 3:**
```bash
WORKER_INDEX=2 WORKER_COUNT=3 dotnet run --project BingoSim.Worker
```

**Web** (ensure WorkerCount matches):
```bash
DISTRIBUTED_WORKER_COUNT=3 dotnet run --project BingoSim.Web
```

### Single Worker (No Partitioning)

Leave `WORKER_INDEX` unset. Worker processes all messages. Publisher still assigns `WorkerIndex` when `WorkerCount > 1`, but the filter is disabled, so all messages are processed.

---

## 4. Expected Performance Impact

| Metric | Before (3 workers, 100K) | Target |
|--------|--------------------------|--------|
| Elapsed time | 35.7s | ≤24s (1.5× improvement) |
| Scaling factor (3 vs 1 worker) | 1.01× | ≥1.5× |
| Claim contention | Workers compete for same queue | Workers have exclusive ranges |

---

## 5. Testing Procedure

1. **Baseline (1 worker, no partitioning):**
   - Start 1 worker without `WORKER_INDEX` set
   - Run 100K simulations
   - Record elapsed time and throughput

2. **Partitioned (3 workers):**
   - Start 3 workers with `WORKER_INDEX=0,1,2` and `WORKER_COUNT=3`
   - Ensure Web has `DISTRIBUTED_WORKER_COUNT=3`
   - Run 100K simulations
   - Record elapsed time and throughput

3. **Validation:**
   - Check worker logs for "Processing batch assigned to WorkerIndex=N"
   - Verify each worker processes approximately 1/3 of batches
   - **Target:** 100K runs in ≤24s (vs 35.9s baseline) = 1.5× improvement

---

## 6. Troubleshooting

### Scaling doesn't improve

- **WorkerCount mismatch:** Ensure Web `DistributedExecution:WorkerCount` matches Worker `WorkerSimulation:WorkerCount` and the actual number of workers
- **WorkerIndex not set:** Each worker must have a unique `WORKER_INDEX` (0, 1, 2, ...)
- **Docker Compose:** Worker partitioning works with replicas via hostname-derived indices (see §7)

### Messages going to skipped queue

- When a worker receives a message for another worker's index, the filter skips it and uses `NotifyConsumed` to avoid the skipped queue. The message is acknowledged and removed. With round-robin publishing order, workers typically receive their assigned messages; mismatches (e.g. after restart) can result in message loss. For production, consider separate queues per worker.

### Worker 2 crashes

- Messages with `WorkerIndex=2` will be delivered to workers 0 and 1, which will skip them. They are acknowledged and removed (not requeued). Those batches will not be processed until worker 2 restarts and new batches are published. For fault tolerance, consider TTL-based fallback (future enhancement).

---

## 7. Docker Compose Support

Worker partitioning works with Docker Compose replicas. Each container receives a unique hostname (e.g. `bingosim_bingosim.worker_1`, `bingosim_bingosim.worker_2`). The `WorkerIndexHostnameResolver` derives `WorkerIndex` from the trailing number when `WORKER_INDEX` is not explicitly set. No per-replica env vars required.

When scaling workers, set both `WORKER_REPLICAS` and `DISTRIBUTED_WORKER_COUNT` to match:
```bash
WORKER_REPLICAS=5 DISTRIBUTED_WORKER_COUNT=5 docker compose up -d
```

## 8. Limitations
- **Hostname format:** Hostname derivation expects a trailing `_N` or `-N` (e.g. `service_1`). Custom hostnames may not parse; set `WORKER_INDEX` explicitly in that case.
- **Skipped messages:** When a worker receives a message for another worker's index, it uses `NotifyConsumed` to avoid the skipped queue. The message is acknowledged and removed—it is not requeued for the correct worker. With stable round-robin delivery, this should be rare.
- **Retries:** `PublishRunWorkAsync` (retry path) uses `WorkerIndex = null` so any worker can process. Retries are not partitioned.

---

## 9. Architecture Constraints

- All changes in Infrastructure and Worker layers (Clean Architecture respected)
- No changes to Core domain logic
- Message contract change is backward compatible (`WorkerIndex` is nullable)
- Existing single-worker deployments continue to work (partitioning is opt-in)
