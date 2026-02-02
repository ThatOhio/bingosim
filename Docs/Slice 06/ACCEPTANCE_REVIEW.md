# Acceptance Test Review - Results Discovery (Slice 6)

## Review Date
2026-02-02

## Source
`Docs/06_Acceptance_Tests.md` — Section "7) Results & Aggregations" (results viewing expectations), Section "9) UI Expectations" (results page behavior)

---

## Acceptance Criteria Verification

### ✅ Section 7: Results & Aggregations

- [x] Batch-level aggregates are stored and displayed (unchanged; Slice 5)
- [x] UI does not recompute aggregates; uses stored aggregates (unchanged)
- [x] **Results discovery:** Users can browse existing batches without knowing a BatchId; Batches list page shows recent batches with status, timestamps, event name, run counts, seed; each row links to the existing Batch Results page

### ✅ Section 9: UI Expectations

- [x] **Simulation Results** supports viewing one batch at a time (existing Results page unchanged)
- [x] **Simulation Results** entry point: persistent nav link "Simulation Results" opens the Batches list page
- [x] Batches list: recent SimulationBatches (newest first); columns BatchId, CreatedAt, Status, Event name, RunCount, CompletedCount, FailedCount, Seed, Execution mode
- [x] Each batch row links to `/simulations/results/{BatchId}` (existing Batch Results page)
- [x] Minimal filtering: Status filter; optional search by Event name
- [x] Performance: Top N (e.g. 50); UI does not hang with many batches

---

## Implementation Summary

| Area | Implementation |
|------|----------------|
| **Application** | BatchListRowDto, ListBatchesRequest, ListBatchesResult; IListBatchesQuery; ISimulationBatchService.GetBatchesAsync (delegates to IListBatchesQuery) |
| **Infrastructure** | ListBatchesQuery (IListBatchesQuery): two-query approach (batches + snapshot; run counts by batch); event name from EventConfigJson; optional structured logging |
| **Web** | Batches.razor at `/simulations/results` (list); Status filter + Event name search; table with link to `/simulations/results/{BatchId}`; nav link "Simulation Results" in MainLayout |
| **Tests** | SimulationBatchServiceTests.GetBatchesAsync_DelegatesToQuery_ReturnsResult; ListBatchesQueryIntegrationTests (Postgres Testcontainers): empty list, event name/status/counts/seed, ordering, StatusFilter, EventNameSearch, Top |

---

## Test Coverage

- **Unit (Application):** SimulationBatchServiceTests — GetBatchesAsync delegates to IListBatchesQuery and returns result; mapping (status, counts, event name) covered by integration test
- **Integration (Infrastructure):** ListBatchesQueryIntegrationTests — ExecuteAsync_EmptyDb_ReturnsEmptyList; ExecuteAsync_WithBatchAndSnapshot_ReturnsEventNameStatusCountsAndSeed; ExecuteAsync_OrderedByCreatedAtDesc_NewestFirst; ExecuteAsync_StatusFilter_ReturnsOnlyMatchingBatches; ExecuteAsync_EventNameSearch_FiltersByEventName; ExecuteAsync_RespectsTop
- **Web:** Routing and pages compile; no bUnit tests added (optional per plan)

---

## Post-Review Notes

- **No schema changes:** List uses existing SimulationBatches, EventSnapshots, SimulationRuns; event name derived from EventSnapshot.EventConfigJson at query time
- **TotalCount omitted:** v1 uses Top N only; TotalCount not returned unless effectively free later
- **Simulation execution / aggregates:** Unchanged; no modifications to execution, seeding, strategies, or persistence models
