# Phase 3A — Claim Reduction Strategy (Design)

**Date:** February 3, 2025  
**Status:** Design Only — No Implementation  
**Context:** Phase 1 and Phase 2 implemented. Phase 2 proved claim DB round-trips dominate throughput; snapshot load is negligible. PostgreSQL processes ~1000 claims/10s regardless of worker count. Scaling workers does not increase aggregate throughput.

---

## 1. Goal

Enable true horizontal scaling by **reducing claim-related DB round-trips per run**.

We must change *how* claims are performed, not merely tune latency.

---

## 2. Current State (Authoritative)

### 2.1 Per-Run DB Round-Trips (Distributed Path)

| Phase | Operation | Round-Trips | Notes |
|-------|-----------|-------------|-------|
| Load | `GetByIdAsync(runId)` | 1 | Fetch run entity (BatchId, Seed, Status, etc.) |
| Claim | `TryClaimAsync(runId, startedAt)` | 1 | Atomic Pending→Running via `ExecuteUpdateAsync` |
| Snapshot | `GetByBatchIdAsync` or cache hit | 0–1 | Phase 2: cache hit ≈ run count; load ≈ 1 per batch |
| Persist | BufferedRunResultPersister flush | Batched | ~50 runs per flush |

**Total per run (steady state):** 2 DB round-trips (GetById + TryClaim). Snapshot is cached.

### 2.2 Bottleneck Evidence (Phase 2 Test Results)

- **1 worker:** ~1000 runs/10s; claim ~13–14 ms avg; claim total ~14,000 ms/10s
- **3 workers:** ~990 runs/10s aggregate; each worker ~330 runs/10s; claim ~14 ms avg per worker
- **Conclusion:** PostgreSQL processes ~1000 claims/10s total. Workers split the load; adding workers does not increase aggregate throughput.

### 2.3 Message Contract

```csharp
public record ExecuteSimulationRun
{
    public required Guid SimulationRunId { get; init; }
}
```

Identifier-only. Web publishes via `PublishBatch(runIds.Select(id => new ExecuteSimulationRun { SimulationRunId = id }))`.

---

## 3. Option Analysis

### Option A — Batch Claiming (Single Message → Batch Claim)

**Description:** Worker receives one `ExecuteSimulationRun` message per run (unchanged). Before processing, worker adds runId to a local "claim buffer." A background loop periodically claims the buffer contents in one `ClaimBatchAsync` call, then processes claimed runs and acks their messages.

**Required code changes:**
- Add `ClaimBatchAsync(IReadOnlyList<Guid> runIds, DateTimeOffset startedAt)` to `ISimulationRunRepository` and `SimulationRunRepository`. Single `ExecuteUpdateAsync` with `WHERE Id IN (...) AND Status = Pending`.
- Worker: New component (e.g. `ClaimBuffer` or `BatchClaimCoordinator`) that aggregates runIds, triggers batch claim, and hands claimed IDs to executors.
- Consumer: Does not call executor directly; instead enqueues runId to buffer. Ack semantics become complex: ack only after run completes, but claim happens in batch before any run completes.
- Executor: New overload or path that accepts pre-claimed run (skips TryClaimAsync). Or executor is invoked only after batch claim succeeds.

**Risks:**
- **Ack coupling:** MassTransit acks when `Consume` returns. If we buffer and return before processing, we ack before execution — message loss on worker crash. If we hold message until processing completes, we block the consumer and reduce effective prefetch.
- **Partial claim failure:** Batch claim returns subset of IDs (some already claimed). Need to route only successfully claimed IDs to execution; unclaimed IDs must be re-published or left for another worker. Adds complexity.
- **Ordering:** Runs in a batch may complete out of order; BufferedRunResultPersister and finalization are order-agnostic, so this is acceptable.
- **Buffer sizing:** Too small → many batch claims, less reduction. Too large → latency, memory, and more IDs per `IN` clause (PostgreSQL limits).

**Impact on correctness:**
- Claim remains atomic (batch `ExecuteUpdateAsync` is atomic). No double execution if we only process successfully claimed IDs.
- Retry: If worker crashes after claim but before persist, runs stay Running. BatchFinalizer or stuck-run reset would need to handle. Current `ResetStuckRunsToPendingAsync` exists for this.

**Impact on throughput:**
- Claim round-trips: 1000 → 100 per 10s (batch size 10) or 50 (batch size 20). Significant reduction.
- GetByIdAsync: Unchanged (1 per run). Still 1000 round-trips/10s.

**Migration complexity:** High. Requires rethinking consumer/executor flow, ack semantics, and buffer lifecycle. Local mode unaffected (different path).

---

### Option B — Batch Message Contract

**Description:** New message type `ExecuteSimulationRunBatch { RunIds[] }`. Web publishes one message per N runs (e.g. 10–20). Worker consumes batch message, calls `ClaimBatchAsync` once, processes claimed runs (sequentially or in parallel), acks one message.

**Required code changes:**
- New message: `ExecuteSimulationRunBatch { IReadOnlyList<Guid> SimulationRunIds }` in BingoSim.Shared.
- Web: `SimulationBatchService` / `PublishRunWorkBatchAsync`: Instead of `PublishBatch` of N single-run messages, chunk runIds into batches of K (e.g. 10) and publish `ExecuteSimulationRunBatch` per chunk.
- New consumer: `ExecuteSimulationRunBatchConsumer` implementing `IConsumer<ExecuteSimulationRunBatch>`.
- Repository: Add `ClaimBatchAsync(runIds, startedAt)` returning `IReadOnlyList<Guid>` (successfully claimed IDs).
- Executor: Invoked per claimed run; can use existing `ExecuteAsync(runId)` or new path that skips GetById if we enrich later.
- Retry: Batch fails → re-publish `ExecuteSimulationRunBatch` with same RunIds. Partial completion: some runs completed, some not. Need idempotent handling — executor skips terminal runs. ClaimBatchAsync only claims Pending; already Running/Completed are no-ops in claim.

**Risks:**
- **Message size:** 10–20 Guids per message is trivial. 100+ may need chunking.
- **Partial batch failure:** If 5 of 10 runs fail, we re-publish batch or individual runs? Re-publishing batch is simpler; ClaimBatchAsync will no-op for already-claimed. Failed runs (MarkFailed) stay Failed; retry would need individual run re-publish. Current retry uses `PublishRunWorkAsync(runId)` — single run. For batch path, we could re-publish `ExecuteSimulationRunBatch` with only the failed run IDs (or single-run message for retries).
- **Worker imbalance:** Large batches may land on few workers; smaller batches spread better. Tune batch size (e.g. 10–20) for balance.
- **Backward compatibility:** Old workers consuming `ExecuteSimulationRun` would need to coexist during rollout, or we cut over entirely. Simpler to cut over: remove single-run publish, only publish batch.

**Impact on correctness:**
- Claim: `ClaimBatchAsync` atomically updates `WHERE Id IN (...) AND Status = Pending`. Same semantics as N individual TryClaimAsync.
- At-least-once: Batch message retried on failure; ClaimBatchAsync idempotent for already-claimed runs.
- Finalization: Unchanged; BufferedRunResultPersister and BatchFinalizer work per run.

**Impact on throughput:**
- Claim round-trips: 1000 → 50–100 per 10s (batch 10–20). Major reduction.
- GetByIdAsync: Unchanged. Executor still loads run per ID. 1000 round-trips/10s.

**Migration complexity:** Medium. New message type, new consumer, Web publish logic change. Clear boundaries. Can run both consumers during transition if desired.

---

### Option C — Message Enrichment

**Description:** Enrich `ExecuteSimulationRun` with BatchId, Seed, and any other immutable run data needed for execution. Executor skips `GetByIdAsync` when message is enriched. Claim still required (1 per run); no reduction in claim round-trips.

**Required code changes:**
- Extend `ExecuteSimulationRun`: Add optional `Guid? SimulationBatchId`, `string? Seed`, `int? RunIndex`, `int? AttemptCount`. If present, executor uses them; if null, fallback to GetByIdAsync (backward compat).
- Web: When publishing, populate from run entity. `PublishBatch` already has runIds; need runs or run data. `SimulationBatchService` has `runs` before publish — can include BatchId, Seed in message. RunIndex and AttemptCount: AttemptCount is 0 at start; RunIndex for logging.
- Executor: New code path: if message has BatchId and Seed, construct minimal `SimulationRun`-like object (or use a DTO) for execution, skip GetByIdAsync. Still must call TryClaimAsync (need runId). TryClaimAsync only needs runId and startedAt — no change.
- Retry: Worker re-publishes via `PublishRunWorkAsync`. Current contract only has runId. Enriched path: retry would need to either (a) re-fetch run and publish enriched, or (b) publish runId-only and fall back to GetByIdAsync. Retry path typically has AttemptCount > 0; executor needs that for "is retry" logic. So retry may still need GetByIdAsync unless we add AttemptCount to message and update it on retry. Complexity grows.

**Risks:**
- **Staleness:** Seed and BatchId are immutable at batch start. AttemptCount changes on retry. If we don't include AttemptCount, executor must load for retries. If we do, retry publisher must know current AttemptCount.
- **Contract growth:** Message gets larger. Still small (few GUIDs + string).
- **Dual path:** Enriched vs non-enriched. More branches, tests.

**Impact on correctness:**
- Seed must match DB. Web has authoritative run at publish time. For retries, worker has run in memory after failed execution — can re-publish with updated AttemptCount if we add it to contract.
- Claim: Unchanged. Still 1 per run.

**Impact on throughput:**
- GetByIdAsync: 1000 → 0 per 10s (for initial batch; retries may still need it). Saves ~1000 round-trips.
- Claim: Unchanged. 1000 round-trips/10s. **Claim remains bottleneck.**

**Migration complexity:** Low–Medium. Add optional fields, executor branch. Backward compatible.

**Conclusion for Option C:** Reduces GetByIdAsync but **not** claim. Phase 2 proved claim dominates. Option C alone does not solve the scaling problem. Useful as a **supplement** to batch claiming.

---

### Option D — Hybrid (Batch Message + Enriched Payload)

**Description:** Combine Option B and C. Publish `ExecuteSimulationRunBatch` with not just RunIds but per-run payload: `{ RunId, BatchId, Seed, RunIndex }`. Worker claims batch once, processes with enriched data, skips GetByIdAsync entirely for the batch.

**Required code changes:**
- New message: `ExecuteSimulationRunBatch { IReadOnlyList<RunWorkItem> Items }` where `RunWorkItem { RunId, BatchId, Seed, RunIndex }`.
- Web: Build RunWorkItems from runs before publish. Chunk into batches of K.
- Consumer: ClaimBatchAsync(runIds). For each claimed RunId, find matching RunWorkItem, execute with enriched data (no GetByIdAsync).
- Executor: Overload `ExecuteAsync(runId, batchId, seed, ...)` or accept `RunWorkItem` that bypasses load. Must still validate run exists and is Pending before claim — but claim batch does that. After claim, we know run is ours; we have Seed from message.
- Retry: Failed runs — re-publish single `ExecuteSimulationRun` (or small batch) with enriched data. Worker's `PublishRunWorkAsync` would need to support enriched single-run for retries.

**Risks:**
- All risks of B and C combined.
- Message size: K items × (Guid + Guid + string + int) ≈ 50–100 bytes per item. 20 items ≈ 2 KB. Acceptable.
- Retry enrichment: Worker needs to re-publish with Seed etc. for single-run retry. Executor's retry path calls `workPublisher.PublishRunWorkAsync(run.Id)`. Current interface only takes runId. Would need `PublishRunWorkAsync(run)` or enriched overload.

**Impact on correctness:**
- Same as B for claim. Same as C for avoiding GetByIdAsync.
- Retry: Run entity in memory has Seed, BatchId. Can publish enriched single-run. Need to extend `ISimulationRunWorkPublisher` for retry with payload.

**Impact on throughput:**
- Claim: 1000 → 50–100 per 10s (batch 10–20). **Major reduction.**
- GetByIdAsync: 0 per 10s. **Eliminated.**
- **Best of both.**

**Migration complexity:** High. New message, new consumer, executor changes, retry path changes. Most comprehensive.

---

## 4. Recommendation

**Primary approach: Option B — Batch Message Contract**

### Justification

| Criterion | Option B | Option A | Option C | Option D |
|-----------|----------|----------|----------|----------|
| **Claim reduction** | Yes (10–20×) | Yes | No | Yes |
| **Architectural consistency** | Clean: message = unit of work | Complex: decoupled ack/claim | Minimal change | Most complete |
| **Safety** | Simple ack = batch done | Ack semantics fragile | Low risk | Medium risk |
| **Testability** | New consumer, clear boundaries | Buffer/coordinator hard to test | Easy | Moderate |
| **Maintainability** | One new message, one new consumer | Ongoing buffer tuning | Incremental | Most moving parts |
| **Migration** | Medium, clear cutover | High, ack redesign | Low | High |

**Why B over A:** Option A's ack-buffer coupling is inherently complex. Holding messages without ack blocks prefetch; acking before processing risks loss. Option B keeps MassTransit semantics simple: one message = one batch of work, ack when batch is done.

**Why B over C alone:** Option C does not reduce claims. Phase 2 proved claim is the bottleneck. C is valuable as a follow-up (Phase 3B) to eliminate GetByIdAsync after we reduce claims.

**Why B over D:** Option D maximizes throughput but adds the most complexity. We can implement B first, measure, then add enrichment (C) as Phase 3B if GetByIdAsync becomes significant. B delivers the critical path (claim reduction) with lower risk.

### Implementation Order (Recommended)

1. **Phase 3A:** Option B — Batch Message Contract + ClaimBatchAsync.
2. **Phase 3B (optional):** Option C — Message enrichment for batch messages. Add BatchId, Seed to `ExecuteSimulationRunBatch` items; executor skips GetByIdAsync. Lower priority until B is measured.

---

## 5. Success Metrics

### 5.1 Worker Logs (Expected Changes)

| Metric | Phase 2 (Current) | Phase 3 (Target) |
|--------|-------------------|-------------------|
| `claim` count per 10s | ~1000 | ~50–100 (batch 10–20) |
| `claim` total (ms) per 10s | ~14,000 | ~700–1,400 (proportional to count) |
| `claim_avg` (ms) | ~14 | ~14 (per claim; fewer claims) |
| `snapshot_cache_hit` | ~1000 | ~1000 (unchanged) |
| `sim` total | ~10–250 ms | ~10–250 ms (unchanged) |
| `persist` | ~1400 ms | ~1400 ms (unchanged) |

**Key signal:** `claim` count drops by factor of 10–20. `claim` total drops proportionally.

### 5.2 Throughput Expectations

| Scenario | Phase 2 | Phase 3 (Target) |
|---------|---------|------------------|
| 1 worker, 10K runs | ~1000 runs/10s | ~2000–3000 runs/10s (claim no longer bottleneck) |
| 3 workers, 10K runs | ~990 runs/10s aggregate | ~3000–6000 runs/10s aggregate (2–3× per worker) |

**Scaling behavior:** With claim round-trips reduced, PostgreSQL can process more aggregate work. Each worker's claim load drops; 3 workers should achieve 2–3× aggregate throughput vs 1 worker.

### 5.3 Validation Procedure

1. Run 10K distributed with 1 worker. Record: runs/10s, claim count, claim total.
2. Run 10K distributed with 3 workers. Record: aggregate runs/10s, per-worker claim count.
3. Compare: 3 workers should show ~2–3× aggregate throughput vs 1 worker.
4. Verify: claim count ≈ (runs / batch_size) per 10s window.

---

## 6. Open Questions / Risks

### 6.1 Batch Size Tuning

- **Recommended starting point:** 10–20 runs per batch. Smaller = more claim round-trips, larger = more latency per message and potential imbalance.
- **Open:** Measure with 10, 15, 20; pick based on throughput and worker distribution.

### 6.2 Retry Semantics for Batch

- **Partial failure:** If 2 of 10 runs fail (non-terminal, retryable), re-publish `ExecuteSimulationRunBatch` with those 2 IDs? Or single-run messages? Single-run keeps retry path simple; batch of 2 is also fine.
- **Recommendation:** Re-publish `ExecuteSimulationRunBatch` with failed run IDs only. Keeps one code path. For single run, batch of 1 is valid.

### 6.3 Local Mode

- Local mode uses `EnqueueBatchAsync`, not MassTransit. Unaffected by batch message change.
- Ensure `RoutingSimulationRunWorkPublisher` or equivalent does not need batch support for local. Local path is separate.

### 6.4 Backward Compatibility

- **Cutover:** Remove `ExecuteSimulationRun` consumer and publish path. All distributed traffic uses `ExecuteSimulationRunBatch`. No coexistence period required if we deploy Web and Worker together.
- **Alternative:** Support both during rollout. Web publishes batch; old workers ignore (no consumer). New workers consume batch. Requires both consumers registered until cutover.

### 6.5 GetByIdAsync After Batch Claim

- After `ClaimBatchAsync`, we have run IDs. Executor still needs Seed, BatchId for each. Options:
  - **A:** Keep GetByIdAsync per run (2 round-trips → 1; claim batched). Still 1000 GetByIdAsync/10s.
  - **B:** Enrich batch message with RunWorkItems (Option D). Eliminates GetByIdAsync.
- **Recommendation for Phase 3A:** Keep GetByIdAsync. Simpler. Measure. If GetByIdAsync becomes significant (e.g. 20%+ of time), add enrichment in Phase 3B.

### 6.6 ClaimBatchAsync Return Value

- Should return `IReadOnlyList<Guid>` of successfully claimed IDs. Some may already be claimed by another worker. Process only returned IDs.
- SQL: `UPDATE SimulationRuns SET Status = 'Running', ... WHERE Id IN (...) AND Status = 'Pending' RETURNING Id`. PostgreSQL supports RETURNING. Use that to get claimed IDs without extra round-trip.

---

## 7. References

- [Distributed_Performance_Plan.md](Distributed_Performance_Plan.md) — Phase 3 section, batch claiming options
- [Phase 2 Test Results](Phase%202%20Test%20Results.md) — Claim dominance evidence
- [Phase_2_Implementation_Summary.md](Phase_2_Implementation_Summary.md) — Snapshot cache, claim observability
- [PERF_NOTES.md](../PERF_NOTES.md) — Benchmark procedure
- `BingoSim.Application/Services/SimulationRunExecutor.cs` — Current flow: GetById → TryClaim → snapshot → ExecuteWithSnapshot
- `BingoSim.Infrastructure/Persistence/Repositories/SimulationRunRepository.cs` — TryClaimAsync implementation
- `BingoSim.Shared/Messages/ExecuteSimulationRun.cs` — Current message contract
