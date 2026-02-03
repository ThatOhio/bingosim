# Slice 6: Results Discovery — Review Summary

## Scope Delivered

- **Navigation:** Persistent nav link "Simulation Results" to `/simulations/results` (Batches list page).
- **Batches list page:** Route `/simulations/results`; table of recent SimulationBatches (newest first, top 50); columns: Batch (link), Created, Status, Event, Runs, Completed, Failed, Seed, Mode.
- **Filtering:** Status dropdown (All / Pending / Running / Completed / Error); optional Event name search; Apply button.
- **Each row** links to existing Batch Results page `/simulations/results/{BatchId}`.
- **Application:** IListBatchesQuery (interface in Application, implementation in Infrastructure); GetBatchesAsync on ISimulationBatchService; DTOs BatchListRowDto, ListBatchesRequest, ListBatchesResult (Items only; no TotalCount in v1).
- **Data:** Two-query approach (batches + snapshot; run counts by batch); event name from EventSnapshot.EventConfigJson; no schema changes.
- **Performance:** Top N (default 50); no full table load.

## Acceptance Alignment

| Doc Section | Status |
|-------------|--------|
| 7) Results & Aggregations | ✅ Stored aggregates unchanged; results discoverable via Batches list |
| 9) UI (Results) | ✅ Simulation Results nav link; Batches list with key columns; link to single-batch Results page; minimal filtering; performant (top N) |

## How to Run

- **Web:** `dotnet run --project BingoSim.Web`; click "Simulation Results" in nav → Batches list; click a batch row → single Batch Results page.
- **Tests:** `dotnet test` (full solution); ListBatchesQuery integration tests use Postgres Testcontainers.

## Files Changed (Summary)

- **Application:** BatchListRowDto, ListBatchesRequest, ListBatchesResult; IListBatchesQuery; ISimulationBatchService + GetBatchesAsync; SimulationBatchService (IListBatchesQuery, GetBatchesAsync).
- **Infrastructure:** ListBatchesQuery (Queries/); DependencyInjection (IListBatchesQuery → ListBatchesQuery).
- **Web:** Batches.razor, Batches.razor.css; MainLayout.razor (nav link "Simulation Results").
- **Tests:** SimulationBatchServiceTests (IListBatchesQuery mock; GetBatchesAsync_DelegatesToQuery_ReturnsResult); ListBatchesQueryIntegrationTests (6 tests, Postgres Testcontainers).
