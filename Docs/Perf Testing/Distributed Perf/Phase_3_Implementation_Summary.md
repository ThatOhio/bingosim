# Phase 3 Implementation Summary

**Date:** February 3, 2025  
**Status:** Implemented  
**Phase:** Batch Message Contract + ClaimBatchAsync (Option B)

---

## What Changed

### A) New Message Contract

- **ExecuteSimulationRunBatch** (`BingoSim.Shared/Messages/ExecuteSimulationRunBatch.cs`)
  - `IReadOnlyList<Guid> SimulationRunIds` — batch of run IDs
  - Replaces `ExecuteSimulationRun` for distributed execution

### B) Web: Batch Publishing

- **MassTransitRunWorkPublisher** now publishes `ExecuteSimulationRunBatch` instead of `ExecuteSimulationRun`
- **DistributedExecution:BatchSize** (default 10) — chunks run IDs; one batch message per chunk
- **PublishRunWorkAsync** (retries) publishes batch of 1: `ExecuteSimulationRunBatch { SimulationRunIds = [runId] }`
- Config: `appsettings.json` + env `DISTRIBUTED_BATCH_SIZE`

### C) Worker: Batch Consumer

- **ExecuteSimulationRunBatchConsumer** — consumes `ExecuteSimulationRunBatch`
- Flow: receive batch → `ClaimBatchAsync(runIds)` → for each claimed ID, `executor.ExecuteAsync(runId, skipClaim: true)` → ack
- Runs not returned from `ClaimBatchAsync` are skipped (already claimed elsewhere)
- **ExecuteSimulationRunConsumer** removed; only batch consumer registered

### D) Repository: ClaimBatchAsync

- **ISimulationRunRepository.ClaimBatchAsync(runIds, startedAt)** — returns `IReadOnlyList<Guid>` of successfully claimed IDs
- Single SQL `UPDATE ... WHERE Id = ANY(ids) AND Status = 'Pending' RETURNING Id`
- Idempotent; concurrency-safe

### E) Executor: skipClaim Overload

- **ISimulationRunExecutor.ExecuteAsync(runId, ct, skipClaim)** — when `skipClaim: true`, skips `TryClaimAsync` (run already claimed by batch)
- Batch consumer invokes with `skipClaim: true`

### F) Observability

- **claim** count = batch claims (1 per batch), not per-run
- **runs_claimed** — count of runs successfully claimed per batch
- Throughput logs unchanged; `claim_avg` computed from batch claim total/count

---

## Why Batch Messaging (Option B)

- **Simple ack semantics:** One message = one batch; ack when batch done
- **Clear boundaries:** New message type, new consumer; no buffer/coordinator complexity
- **Testability:** Integration tests updated; ClaimBatchAsync has dedicated tests
- **Retry:** Batch of 1 valid; failed runs re-published as `ExecuteSimulationRunBatch { [runId] }`

---

## Known Risks

| Risk | Mitigation |
|------|------------|
| Batch size too small | More claim round-trips; tune via `DistributedExecution:BatchSize` (try 15–20) |
| Batch size too large | Latency per message; potential worker imbalance |
| Partial batch failure | Re-publish batch with failed IDs only; ClaimBatchAsync idempotent for already-claimed |
| GetByIdAsync still 1 per run | Deferred; Phase 3B enrichment if it becomes significant |

---

## Follow-up Candidates

- **Message enrichment (Phase 3B):** Add BatchId, Seed to batch message items; executor skips GetByIdAsync
- **Batch size tuning:** Measure with 10, 15, 20; pick based on throughput and scaling
- **Connection pool:** Monitor if 3 workers × 4 concurrent batches stress connections

---

## References

- [Phase_3_Claim_Strategy.md](Phase_3_Claim_Strategy.md) — Design and option analysis
- [Distributed_Performance_Plan.md](Distributed_Performance_Plan.md) — Phase 3 section
- [PERF_NOTES.md](../PERF_NOTES.md) — Benchmark procedure and Phase 3 expectations
