# Slice 6: Results Discovery (Batch List + Nav) — Complete

## Delivered Scope

- **Navigation:** Persistent nav link "Simulation Results" to `/simulations/results` (Batches list page).
- **Batches list page:** Displays recent SimulationBatches (newest first, top 50); columns BatchId (link), CreatedAt, Status, Event name, RunCount, CompletedCount, FailedCount, Seed, Execution mode; Status filter and optional Event name search; each row links to existing Batch Results page `/simulations/results/{BatchId}`.
- **Application:** IListBatchesQuery (interface); BatchListRowDto, ListBatchesRequest, ListBatchesResult (Items only; no TotalCount in v1); ISimulationBatchService.GetBatchesAsync delegates to IListBatchesQuery.
- **Infrastructure:** ListBatchesQuery implements IListBatchesQuery; two-query approach (batches + EventSnapshot for event name; run counts by SimulationBatchId); event name from EventConfigJson (fallback batch name); optional structured logging (LogDebug).
- **No changes** to simulation execution, seeding, strategies, or persistence models; no recomputation of aggregates.

---

## File List Changed

### Created

**Application**
- `BingoSim.Application/DTOs/BatchListRowDto.cs`
- `BingoSim.Application/DTOs/ListBatchesRequest.cs`
- `BingoSim.Application/DTOs/ListBatchesResult.cs`
- `BingoSim.Application/Interfaces/IListBatchesQuery.cs`

**Infrastructure**
- `BingoSim.Infrastructure/Queries/ListBatchesQuery.cs`

**Web**
- `BingoSim.Web/Components/Pages/Simulations/Batches.razor`
- `BingoSim.Web/Components/Pages/Simulations/Batches.razor.css`

**Tests**
- `Tests/BingoSim.Infrastructure.IntegrationTests/Queries/ListBatchesQueryIntegrationTests.cs`

### Modified

**Application**
- `BingoSim.Application/Interfaces/ISimulationBatchService.cs` — added GetBatchesAsync
- `BingoSim.Application/Services/SimulationBatchService.cs` — IListBatchesQuery dependency; GetBatchesAsync implementation

**Infrastructure**
- `BingoSim.Infrastructure/DependencyInjection.cs` — registered IListBatchesQuery → ListBatchesQuery

**Web**
- `BingoSim.Web/Components/Layout/MainLayout.razor` — nav link "Simulation Results" to `/simulations/results`

**Tests**
- `Tests/BingoSim.Application.UnitTests/Services/SimulationBatchServiceTests.cs` — IListBatchesQuery mock; GetBatchesAsync_DelegatesToQuery_ReturnsResult; existing test updated for new constructor parameter

---

## Migration Notes

**None.** No database migrations; no schema changes. List uses existing SimulationBatches, EventSnapshots, and SimulationRuns.

---

## Commands

```bash
dotnet test
```

---

## Manual UI Verification

1. **Run Web:** `dotnet run --project BingoSim.Web`
2. **Nav:** In the sidebar, confirm "Simulation Results" link is present and points to `/simulations/results`.
3. **Batches list:** Navigate to Simulation Results; confirm Batches list page loads (empty or with existing batches).
4. **Filters:** Select a status (e.g. Completed) and/or enter an event name search; click Apply; confirm list updates.
5. **Detail:** If any batches exist, click a batch row link; confirm navigation to `/simulations/results/{BatchId}` (existing Batch Results page).
6. **Run Simulations:** From Run Simulations, start a batch; from Simulation Results list, confirm the new batch appears and its row links to the correct Results page.
