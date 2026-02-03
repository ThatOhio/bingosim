# Implementation 02: Refresh and Transitions

**Slice:** Slice 2 — Auto-Refresh UX  
**Date:** 2025-02-03

---

## 1. RefreshIndicator API

**Location:** `BingoSim.Web/Components/Shared/RefreshIndicator.razor`

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `LastUpdated` | `DateTimeOffset?` | — | When data was last refreshed; `null` displays "—" |
| `IsPaused` | `bool` | — | When `true`, auto-refresh is paused; Pause button shows "Resume" |
| `IsRefreshing` | `bool` | — | When `true`, Refresh button is disabled and shows "Refreshing…" |
| `IntervalSeconds` | `int` | `5` | Displayed interval (e.g., "every 5 s") |
| `OnRefresh` | `EventCallback` | — | Invoked when user clicks Refresh |
| `OnPauseToggle` | `EventCallback` | — | Invoked when user clicks Pause/Resume |

### UI Elements

- **Last updated:** Formatted timestamp or "—"
- **Interval:** "(every N s)"
- **Refresh button:** Triggers `OnRefresh`; disabled when `IsRefreshing`
- **Pause/Resume button:** Toggles `IsPaused` via `OnPauseToggle`

---

## 2. Timer Lifecycle and Pause Behavior

### Ownership

The **parent page** owns the timer. `RefreshIndicator` is presentational only; it does not create or manage timers.

### Lifecycle

1. **Start:** When a load completes and the page has active/running data, the parent starts a `System.Threading.Timer` with the configured interval.
2. **Tick:** On each tick, the parent invokes the load logic (non-blocking). If paused or a refresh is already in progress, the tick is ignored.
3. **Pause:** User clicks Pause → parent sets `_refreshPaused = true`, disposes the timer. No further ticks occur.
4. **Resume:** User clicks Resume → parent sets `_refreshPaused = false`, starts a new timer if data is still active/running.
5. **Dispose:** When the component is disposed, the timer is disposed.

### Pause Behavior

- **Pause** stops the timer completely. No background refreshes occur until Resume.
- **Resume** restarts the timer with a clean interval (first tick after `IntervalSeconds`).

---

## 3. Overlapping Refresh Prevention

### Mechanism

Each page uses a `_backgroundRefreshInProgress` flag:

1. When a background refresh (timer or manual) starts, the flag is set to `true`.
2. If another refresh is requested while the flag is `true`, the request is ignored (timer tick returns early; manual Refresh button is disabled via `IsRefreshing`).
3. When the refresh completes, the flag is set to `false`.

### Batches.razor

- `_backgroundRefreshInProgress` guards both timer ticks and manual refresh.
- Timer callback (`TimerTickAsync`): returns immediately if `_refreshPaused || _backgroundRefreshInProgress`.
- Manual refresh (`ManualRefreshAsync`): returns immediately if `_backgroundRefreshInProgress`.
- `LoadBatchesAsync(isBackgroundRefresh: true)`: returns immediately if `_backgroundRefreshInProgress` at entry.

### SimulationResults.razor

- Same pattern: `_backgroundRefreshInProgress` guards timer and manual refresh.
- `LoadAsync(isBackgroundRefresh: true)`: returns immediately if `_backgroundRefreshInProgress` at entry.

### StateHasChanged

- `StateHasChanged` is called once at the end of each load (initial or refresh).
- No extra calls during refresh; no spam.

---

## 4. Integration Summary

### Batches.razor

- **When shown:** `_hasActiveBatches && _hasLoadedOnce`
- **Interval:** 5 seconds
- **LastUpdated:** Set when `LoadBatchesAsync` completes (any mode)
- **Manual refresh:** Calls `LoadBatchesAsync(isBackgroundRefresh: true)` — non-blocking, does not wipe content

### SimulationResults.razor

- **When shown:** Batch status is Running or Pending
- **Interval:** 3 seconds
- **LastUpdated:** Set when `LoadAsync` completes (any mode)
- **Manual refresh:** Calls `LoadAsync(isBackgroundRefresh: true)` — non-blocking, does not set `_loading`

---

## 5. Refresh Behavior Rules (Verified)

| Rule | Implementation |
|------|----------------|
| Initial load uses blocking loading state | `_loading` / `_hasLoadedOnce`; block spinner shown |
| Auto-refresh uses non-blocking indicator only | `_backgroundRefreshInProgress` drives `IsRefreshing`; no block spinner |
| Manual refresh must not reset full-page loading | Manual refresh calls load with `isBackgroundRefresh: true`; `_loading` never set |
| Pause stops timer completely | Timer disposed; no ticks |
| Resume restarts timer cleanly | New timer created with full interval |
