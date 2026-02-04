# Phase 4I - Message Broker Throughput Optimization

**Date:** February 3, 2025  
**Status:** Implementation Complete — Pending Validation  
**Context:** Phase 4H revealed message broker throughput as the bottleneck (~3,000 runs/s ceiling). This phase optimizes batch publishing to increase message delivery rate.

---

## 1. Root Cause (from Phase 4H)

**Evidence:**
- 500K runs: 1 worker 183.8s vs 3 workers 186.5s (0.99× scaling)
- Message delivery rate: ~30,000 runs per 10 seconds — **constant regardless of worker count**
- Workers idle waiting for messages; partitioning and simulation performance are excellent

**Hypothesis:** Sequential batch publishing creates an artificial ceiling. For 500K runs with BatchSize=20:
- Total batches: 25,000
- If each publish takes ~7ms: 25,000 × 7ms ≈ **175 seconds** just to publish

---

## 2. Implementation Summary

### Approach Chosen: Option B (Chunked Parallel Publishing)

**Rationale:** Option B provides explicit control over concurrency and avoids overwhelming RabbitMQ with 25,000 concurrent publish operations. Chunking (100 batches at a time) balances throughput with broker capacity.

### Files Modified

| File | Change |
|------|--------|
| `BingoSim.Infrastructure/Simulation/MassTransitRunWorkPublisher.cs` | Replaced sequential loop with chunked `Task.WhenAll`; added `ILogger` and publish timing |
| `BingoSim.Infrastructure/Simulation/DistributedExecutionOptions.cs` | Added `PublishChunkSize` (default 100) |
| `compose.yaml` | Added `DistributedExecution__PublishChunkSize` for bingosim.web |
| `.env.example` | Documented `DISTRIBUTED_PUBLISH_CHUNK_SIZE` |

### Implementation Details

**MassTransitRunWorkPublisher.PublishRunWorkBatchAsync:**
- Chunks batches into groups of `PublishChunkSize` (default 100)
- For each chunk, starts all publish tasks in parallel via `Task.WhenAll`
- Preserves `WorkerIndex` assignment (deterministic: `batchIndex % workerCount`)
- Logs: `Published {BatchCount} batches ({RunCount} runs) in {ElapsedMs}ms ({AvgMs:F2}ms per batch) [CHUNKED PARALLEL]`

**Configuration:**
- `DistributedExecution:PublishChunkSize` — batches per concurrent publish chunk
- Environment: `DISTRIBUTED_PUBLISH_CHUNK_SIZE` (default 100)

---

## 3. Profiling Results (To Be Filled After Testing)

### Step 1: Sequential Baseline (Pre-Phase 4I)

*If you have pre-change logs from Phase 4H, record here:*

| Metric | Value |
|--------|-------|
| Batches published | 25,000 |
| Total publish time | _[FILL: e.g. 175000ms]_ |
| Avg ms per batch | _[FILL: e.g. 7.00]_ |

### Step 2: Parallel Publishing (Post-Phase 4I)

*After running 500K simulation, check Web logs for:*

```
Published 25000 batches (500000 runs) in _____ms (_____ms per batch) [CHUNKED PARALLEL]
```

| Metric | Value |
|--------|-------|
| Batches published | 25,000 |
| Total publish time | _[FILL]_ |
| Avg ms per batch | _[FILL]_ |
| Improvement vs sequential | _[FILL: e.g. 5-8×]_ |

### Step 3: Worker Throughput Impact

| Configuration | Before (Phase 4H) | After (Phase 4I) |
|---------------|------------------|------------------|
| 1 worker, 500K | 183.8s (2,720 runs/s) | _[FILL]_ |
| 3 workers, 500K | 186.5s (2,681 runs/s) | _[FILL]_ |
| Scaling factor (3 vs 1) | 0.99× | _[FILL]_ |

---

## 4. Test Procedure

1. **Build Web in Release mode:**
   ```bash
   dotnet build BingoSim.Web -c Release
   ```

2. **Start infrastructure:**
   ```bash
   docker compose up -d postgres rabbitmq bingosim.web
   ```

3. **Start 1 worker:**
   ```bash
   WORKER_INDEX=0 WORKER_COUNT=1 dotnet run --project BingoSim.Worker -c Release
   ```

4. **Start 500K run simulation** via Web UI (Event: Spring League Bingo, 2 teams, 500K runs, Distributed mode)

5. **Check Web logs** for publish timing:
   ```
   Published 25000 batches (500000 runs) in Xms (Yms per batch) [CHUNKED PARALLEL]
   ```

6. **Re-test with 3 workers** to validate scaling:
   ```bash
   # Terminal 1
   WORKER_INDEX=0 WORKER_COUNT=3 dotnet run --project BingoSim.Worker -c Release
   # Terminal 2
   WORKER_INDEX=1 WORKER_COUNT=3 dotnet run --project BingoSim.Worker -c Release
   # Terminal 3
   WORKER_INDEX=2 WORKER_COUNT=3 dotnet run --project BingoSim.Worker -c Release
   ```

---

## 5. Tuning Parameters

| Parameter | Default | Recommendation |
|-----------|--------|----------------|
| `PublishChunkSize` | 100 | Start here. If too slow: 200–500. If RabbitMQ errors: 50 |
| `BatchSize` | 20 | Unchanged; 20 balances claim round-trips and message count |

**RabbitMQ monitoring:** Check management UI (http://localhost:15672) for queue depth and publish rate during simulation.

---

## 6. Architecture Compliance

- **Clean Architecture:** Changes confined to Infrastructure layer (`MassTransitRunWorkPublisher`)
- **No Core/Application changes:** Publishing strategy is infrastructure concern
- **Error handling:** MassTransit handles publish failures; consider adding retry logic if needed
- **Cancellation:** `CancellationToken` propagated to all publish tasks
- **WorkerIndex:** Deterministic assignment preserved; batch order irrelevant for simulations

---

## 7. Success Criteria

- [x] Implemented parallel batch publishing (Option B, chunked)
- [x] Added `PublishChunkSize` configuration
- [x] Added publish timing instrumentation
- [ ] Reduced publish time for 25K batches by ≥50% *(validate with test)*
- [ ] Re-tested worker throughput *(validate with test)*
- [ ] Re-tested 3 workers for scaling factor ≥1.5× *(validate with test)*

---

## 8. Expected Outcomes

**If publishing was the bottleneck:**
- Publish time: ~175s → ~20–30s (5–8× faster)
- Message delivery: ~3,000 runs/s → ≥10,000 runs/s
- 3 workers: ≥1.5× scaling over 1 worker

**If publishing was NOT the bottleneck:**
- Parallel publishing won't significantly change throughput
- Investigate: RabbitMQ server capacity, network, serialization, worker prefetch limits

---

## 9. Production Recommendations

1. **Default PublishChunkSize=100** — Safe for most deployments
2. **Monitor RabbitMQ** — Queue depth, publish rate, connection count
3. **Tune per environment** — Higher chunk size if broker has spare capacity
4. **Document** — Record optimal chunk size for production in PERF_NOTES.md
