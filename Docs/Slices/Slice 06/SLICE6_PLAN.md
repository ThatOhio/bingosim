# Slice 6: Results Discovery (Batch List + Nav) — Plan Only

**Scope:** User-friendly discovery and browsing of existing simulation batches without requiring a known BatchId. Add a Batches list page, navigation link, and minimal filtering; use stored aggregates only; no changes to simulation execution, seeding, strategies, or persistence models.

**Source of truth:** `Docs/06_Acceptance_Tests.md` — Section "7) Results & Aggregations" (results viewing expectations), Section "9) UI Expectations" (results page behavior).

**Constraints:** Clean Architecture; Web server-rendered; Application queries only (no direct EF in Web); do NOT change simulation execution, seeding, strategies, or persistence models; do NOT recompute aggregates — use stored aggregates from Slice 5.

---

## 1) Application Query Design (DTO for List Rows)

### 1.1 List Row DTO

Introduce a dedicated DTO for one batch row on the list page:

- **Name:** `BatchListRowDto` (or `SimulationBatchListRowDto`)
- **Location:** `BingoSim.Application/DTOs/BatchListRowDto.cs`
- **Properties:**
  - `Guid BatchId` — primary key; used for link to existing Batch Results page
  - `DateTimeOffset CreatedAt` — batch creation time (display as “Created” or “Started”)
  - `BatchStatus Status` — Running / Completed / Error (and Pending if shown)
  - `string EventName` — from snapshot metadata or stored batch name; empty if unavailable
  - `int RunCount` — requested runs (from `SimulationBatch.RunsRequested`)
  - `int CompletedCount` — number of runs with `RunStatus.Completed`
  - `int FailedCount` — number of runs with `RunStatus.Failed`
  - `string Seed` — batch seed (if stored; already on `SimulationBatch`)
  - `ExecutionMode? ExecutionMode` — Local / Distributed if stored; omit or null when not applicable

All properties are read-only (init). No new domain entities; DTO lives in Application.

### 1.2 List Request and Result

- **Request:** `ListBatchesRequest`
  - **Location:** `BingoSim.Application/DTOs/ListBatchesRequest.cs`
  - **Properties:**
    - `int Top` (e.g. 50 or 100) — cap number of batches returned; **or** `int PageSize` + `int Page` for classic pagination (recommend “top N” for v1 to avoid offset cost)
    - `BatchStatus? StatusFilter` — optional filter by batch status
    - `string? EventNameSearch` — optional search by event name (substring/contains; case-insensitive recommended)
  - No date range in v1 (optional per requirements).

- **Result:** `ListBatchesResult`
  - **Location:** `BingoSim.Application/DTOs/ListBatchesResult.cs`
  - **Properties:**
    - `IReadOnlyList<BatchListRowDto> Items`
    - `int TotalCount` — total matching batches (optional for “top N” only; useful if UI shows “Showing 1–50 of 123”)

### 1.3 Application Contract

- **Option A (recommended):** Extend existing `ISimulationBatchService` with:
  - `Task<ListBatchesResult> GetBatchesAsync(ListBatchesRequest request, CancellationToken cancellationToken = default);`
- **Option B:** New application interface `IListBatchesQuery` with `Task<ListBatchesResult> ExecuteAsync(ListBatchesRequest request, CancellationToken cancellationToken = default);` and have `SimulationBatchService.GetBatchesAsync` delegate to it.

Recommendation: **Option A** — extend `ISimulationBatchService` so all batch-related reads stay in one place. The implementation of “list batches” (which needs EF and cross-entity data) will live behind a small **Application-level abstraction** implemented in Infrastructure:

- **Application:** Define `IListBatchesQuery` in Application (interface only), returning `ListBatchesResult`. `SimulationBatchService` depends on `IListBatchesQuery` and implements `GetBatchesAsync` by calling it. No EF in Application.
- **Infrastructure:** Implement `IListBatchesQuery` (e.g. `ListBatchesQuery`) using `AppDbContext` to build the list (see section 2).

---

## 2) Data Retrieval Approach (Tables, Columns, Event Name)

### 2.1 Tables and Columns Used

- **SimulationBatches:** `Id`, `EventId`, `Name`, `RunsRequested`, `Seed`, `ExecutionMode`, `Status`, `CreatedAt`, `CompletedAt`, `ErrorMessage`. No schema change.
- **EventSnapshots:** `SimulationBatchId`, `EventConfigJson` (JSONB). Used to derive **event name** (see below).
- **SimulationRuns:** `SimulationBatchId`, `Status`. Used to compute **CompletedCount** and **FailedCount** per batch.

No new tables or columns. No changes to SimulationBatch, EventSnapshot, or SimulationRun entity definitions or migrations.

### 2.2 Obtaining Event Name

- **Source:** `EventSnapshot.EventConfigJson` — serialized `EventSnapshotDto` (see `BingoSim.Application/Simulation/Snapshot/EventSnapshotDto.cs`) which includes `EventName`.
- **Approach:** In the Infrastructure implementation of `IListBatchesQuery`:
  1. Query batches (with optional join to `EventSnapshots` on `SimulationBatchId = SimulationBatch.Id`).
  2. For each batch, read `EventSnapshot.EventConfigJson` and deserialize to a minimal type (e.g. `{ "EventName": "..." }`) or use `System.Text.Json` to read only the `EventName` property to avoid full DTO dependency in Infrastructure. Alternatively, if PostgreSQL is used, EF Core / Npgsql may support querying JSON properties (e.g. `EventConfigJson->>'EventName'`) for filtering and projection; if so, event name can be selected in the same query without full deserialization.
  3. **Fallback:** If snapshot is missing or JSON parse fails, use `SimulationBatch.Name` if non-empty, otherwise display empty or “(Unknown event)”.

No persistence model change: event name is derived at query time from existing snapshot JSON (or batch name).

### 2.3 Obtaining Run Counts (CompletedCount, FailedCount)

- **Source:** `SimulationRuns` table; `Status` enum: `Pending`, `Running`, `Completed`, `Failed`.
- **Approach:** For the set of batch IDs returned by the “list” query (after applying filters and top N):
  - **Option A:** Single aggregated query in Infrastructure: group by `SimulationBatchId`, count `Status == Completed` and `Status == Failed` (e.g. two conditional counts or two grouped counts). Run this for the batch IDs in the current page/top N only to avoid loading all runs.
  - **Option B:** One query that returns batches + snapshot; then one query: `SimulationRuns` filtered by `SimulationBatchId` in (list of ids), grouped by `SimulationBatchId` with counts. Option B is straightforward and avoids N+1.

Recommendation: **Two queries** in `ListBatchesQuery` implementation:
1. **Query 1:** Batches (with optional left join to EventSnapshot), optional filter by `Status`, optional filter by event name (from JSON or after materializing), ordered by `CreatedAt` descending, then `Take(Top)` (or Skip/Take for pagination). Project to get batch fields + raw `EventConfigJson` (or event name if DB supports JSON projection).
2. **Query 2:** For the batch IDs from Query 1, query `SimulationRuns` grouped by `SimulationBatchId` with counts of `Status == Completed` and `Status == Failed`. Build a dictionary `BatchId -> (CompletedCount, FailedCount)`.
3. Map to `BatchListRowDto`: batch fields + event name from snapshot (or batch name) + run counts from dictionary; default counts to 0 if a batch has no runs yet.

This keeps the list endpoint efficient and avoids loading full run entities for all batches.

### 2.4 Filtering (Minimal)

- **Status:** Apply `Where(b => b.Status == request.StatusFilter)` when `StatusFilter` is not null.
- **Event name search:** When `EventNameSearch` is not null/empty:
  - **If DB supports JSON query:** Filter in the batch+snapshot query (e.g. `EventConfigJson` contains or equals event name).
  - **Otherwise:** After fetching a candidate set (e.g. by status only, ordered by CreatedAt, take a larger N), filter in memory by parsing `EventConfigJson` and matching `EventName` (substring, case-insensitive). To keep performance, cap the candidate set (e.g. fetch up to 200 batches, filter by event name, then take top 50). Prefer moving this into the DB (JSON operator) if available.

Date range filter is **not** required for Slice 6.

---

## 3) Web UI Page Design + Route

### 3.1 Batches List Page

- **Component name:** `Batches.razor` (or `SimulationResultsList.razor`).
- **Route:** `@page "/simulations/results"` — list of batches; no route parameter.
- **Location:** `BingoSim.Web/Components/Pages/Simulations/Batches.razor` (and optional `Batches.razor.css`).
- **Behavior:**
  - On load, call `ISimulationBatchService.GetBatchesAsync(new ListBatchesRequest { Top = 50, StatusFilter = ..., EventNameSearch = ... })` (with optional filters from UI).
  - Display a table (or list) of batches: each row shows BatchId (short or link), CreatedAt, Status, Event name, RunCount, CompletedCount, FailedCount, Seed, Execution mode (if present).
  - Each row is a link to the **existing** Batch Results page: `/simulations/results/{BatchId}` (existing route from Slice 5).
  - Empty state: “No batches found” (or “No batches match your filters”) when `Items.Count == 0`.
  - Optional: Status filter dropdown (All / Pending / Running / Completed / Error), optional event name search text box, “Apply” or auto-apply. No date range in v1.
  - Use pagination or “top N” (e.g. “Show 50” or “Show 100”) so the UI does not load all batches; avoid hang with many batches.

### 3.2 Existing Batch Results Page (Unchanged)

- **Route:** `@page "/simulations/results/{BatchId:guid}"` — existing `SimulationResults.razor`; no change to route or behavior.
- List page links to this URL for each batch.

### 3.3 Routing Summary

| Route                            | Page                 | Purpose                    |
|----------------------------------|----------------------|----------------------------|
| `/simulations/results`           | Batches.razor        | List batches (new)         |
| `/simulations/results/{BatchId}` | SimulationResults.razor | Single batch results (existing) |

Blazor will match the more specific route for `/simulations/results/{guid}` and the base route for `/simulations/results`.

---

## 4) Navigation Update Plan

- **File:** `BingoSim.Web/Components/Layout/MainLayout.razor`
- **Change:** Add a persistent nav link:
  - **Label:** “Simulation Results” or “Batches” (recommend “Simulation Results” to align with acceptance “Simulation Results supports viewing one batch at a time” and list as entry point).
  - **Href:** `/simulations/results` — the new list page.
- **Placement:** After “Run Simulations” (or alongside it) so users can go from “Run Simulations” to “Simulation Results” to browse and open any batch. No removal of existing “Run Simulations” link; “Simulation Results” becomes the entry to browse batches and then open one.

---

## 5) Test Plan (Unit / Integration and Basic UI Routing)

### 5.1 Application Layer (Unit)

- **List batches query / service:**
  - Mock `IListBatchesQuery`: when called with a given `ListBatchesRequest`, return a fixed `ListBatchesResult` with 0, 1, or several `BatchListRowDto` items.
  - Test `SimulationBatchService.GetBatchesAsync`: given request R, verify it calls `IListBatchesQuery` with R (or equivalent) and returns the result’s items and total count unchanged.
- **DTOs:** No logic to test; optional sanity test that request/result and `BatchListRowDto` can be constructed with expected types.

### 5.2 Infrastructure Layer (Integration)

- **ListBatchesQuery implementation:**
  - Given a test database with a few `SimulationBatch` rows (and corresponding `EventSnapshot` and `SimulationRun` rows), call the query with `Top = 10`, no filters.
  - Assert returned items count, order (newest first by CreatedAt), and that each row has correct BatchId, CreatedAt, Status, EventName (from snapshot or batch name), RunCount, CompletedCount, FailedCount, Seed, ExecutionMode where applicable.
  - Test with `StatusFilter = Completed`: only batches with that status.
  - Test with `EventNameSearch = "Winter"`: only batches whose event name (from snapshot or name) contains “Winter” (case-insensitive).
  - Test with empty DB: result is empty list, no throw.

### 5.3 Web Layer (Basic UI / Routing)

- **Batches list page (bUnit or similar):**
  - Resolve `ISimulationBatchService` (mock): `GetBatchesAsync` returns a known `ListBatchesResult` with one or two items.
  - Render the Batches page (route `/simulations/results`).
  - Assert: table or list shows expected batch identifiers and at least one link to `/simulations/results/{BatchId}`.
  - Assert: navigating to `/simulations/results/{guid}` still renders the existing SimulationResults page (same component as before).
- **Navigation:**
  - Assert that `MainLayout` contains a link with href `/simulations/results` and label “Simulation Results” (or “Batches”).

No tests for simulation execution, seeding, strategies, or persistence model changes.

---

## 6) Exact List of Files to Create or Modify

### Create

- **Application**
  - `BingoSim.Application/DTOs/BatchListRowDto.cs`
  - `BingoSim.Application/DTOs/ListBatchesRequest.cs`
  - `BingoSim.Application/DTOs/ListBatchesResult.cs`
  - `BingoSim.Application/Interfaces/IListBatchesQuery.cs`
  - (Optional) `BingoSim.Application/Queries/ListBatchesQuery.cs` — only if the query handler lives in Application with no EF; otherwise the implementation is only in Infrastructure.
- **Infrastructure**
  - `BingoSim.Infrastructure/Queries/ListBatchesQuery.cs` (or `BingoSim.Infrastructure/Persistence/Queries/ListBatchesQuery.cs`) — implements `IListBatchesQuery`, uses `AppDbContext`.
- **Web**
  - `BingoSim.Web/Components/Pages/Simulations/Batches.razor`
  - `BingoSim.Web/Components/Pages/Simulations/Batches.razor.css` (optional)
- **Tests**
  - Unit: e.g. `Tests/BingoSim.Application.UnitTests/Services/SimulationBatchServiceListBatchesTests.cs` (or extend existing `SimulationBatchServiceTests`) for `GetBatchesAsync` with mocked `IListBatchesQuery`.
  - Integration: e.g. `Tests/BingoSim.Infrastructure.IntegrationTests/Queries/ListBatchesQueryIntegrationTests.cs` for `ListBatchesQuery` against test DB.
  - Web: e.g. `Tests/BingoSim.Web.Tests/Pages/BatchesPageTests.cs` (or equivalent) for list page rendering and link to batch detail; optional test for nav link in layout.

### Modify

- **Application**
  - `BingoSim.Application/Interfaces/ISimulationBatchService.cs` — add `Task<ListBatchesResult> GetBatchesAsync(ListBatchesRequest request, CancellationToken cancellationToken = default);`
  - `BingoSim.Application/Services/SimulationBatchService.cs` — inject `IListBatchesQuery`, implement `GetBatchesAsync` by delegating to it.
- **Infrastructure**
  - `BingoSim.Infrastructure/DependencyInjection.cs` — register `IListBatchesQuery` → `ListBatchesQuery` (scoped).
- **Web**
  - `BingoSim.Web/Components/Layout/MainLayout.razor` — add nav link “Simulation Results” (or “Batches”) to `/simulations/results`.

No new or modified files in Core (no entity or repository interface changes for list). No changes to SimulationResults.razor route or logic, RunSimulations, SimulationRunner, EventSnapshotBuilder, seeding, or persistence models/migrations.

---

## Summary

- **Application:** New DTOs (`BatchListRowDto`, `ListBatchesRequest`, `ListBatchesResult`) and `IListBatchesQuery`; extend `ISimulationBatchService` with `GetBatchesAsync` implemented via `IListBatchesQuery`.
- **Data:** List batches from `SimulationBatches` + `EventSnapshots` (event name from `EventConfigJson`) + run counts from `SimulationRuns`; two-query approach in Infrastructure; no schema changes.
- **Web:** New Batches list page at `/simulations/results`, each row linking to `/simulations/results/{BatchId}`; add nav link in `MainLayout`.
- **Filtering:** Status filter and optional event name search; top N (e.g. 50/100) or simple pagination to avoid loading everything.
- **Tests:** Unit (service + mocked query), integration (ListBatchesQuery with test DB), and basic Web (list page + nav + routing to existing results page).

No code is written in this plan; implementation follows in a subsequent step.
