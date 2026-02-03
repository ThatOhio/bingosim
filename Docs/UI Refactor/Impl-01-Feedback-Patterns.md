# Implementation 01: Feedback and Status Patterns

**Slice:** Slice 1 — Feedback and Status  
**Date:** 2025-02-03

---

## 1. Components Added

### LoadingSpinner

**Location:** `BingoSim.Web/Components/Shared/LoadingSpinner.razor`

**Purpose:** Indicates loading state during async operations. Supports inline and block usage with an optional label.

**Parameters:**

| Parameter | Type   | Default | Description                                      |
|-----------|--------|---------|--------------------------------------------------|
| `Inline`  | `bool` | `false` | When `true`, renders inline; when `false`, block (centered, padded) |
| `Label`   | `string?` | `null` | Optional text shown next to the spinner (e.g., "Loading events…") |

**Usage:**
- **Block (initial page load):** `<LoadingSpinner Label="Loading events…" />`
- **Inline (secondary load):** `<LoadingSpinner Inline="true" Label="Loading teams…" />`

**Accessibility:** Uses `aria-busy="true"` and `aria-label` for screen readers.

---

### Alert

**Location:** `BingoSim.Web/Components/Shared/Alert.razor`

**Purpose:** Displays severity-based feedback messages (errors, warnings, info, success). Replaces plain text error display.

**Parameters:**

| Parameter  | Type          | Default         | Description                    |
|------------|---------------|-----------------|--------------------------------|
| `Message`  | `string?`     | `null`          | The message to display         |
| `Severity` | `AlertSeverity` | `AlertSeverity.Error` | Error, Warning, Info, Success |

**Usage:**
- `<Alert Message="@_error" Severity="AlertSeverity.Error" />`
- `<Alert Message="@_batch.ErrorMessage" Severity="AlertSeverity.Error" />`

**Rendering:** Renders only when `Message` is non-null and non-empty. Uses `role="alert"` for accessibility.

---

## 2. Where Loading vs Refresh Indicators Are Used

### Run Simulations (`RunSimulations.razor`)

| Scenario              | Indicator Type | Placement                                      |
|-----------------------|----------------|------------------------------------------------|
| Initial page load     | Block          | Full-page; replaces content until events load   |
| Event/team fetch      | Inline         | Next to "Drafted teams" label when switching events |

**Behavior:** When `_loadingEvents` is true, the entire form is hidden and a block spinner is shown. When the user changes the event, an inline spinner appears next to the teams list until teams load.

---

### Batches / Simulation Results List (`Batches.razor`)

| Scenario              | Indicator Type | Placement                                      |
|-----------------------|----------------|------------------------------------------------|
| Initial page load     | Block          | Full-page; replaces content until first load   |
| Filter apply (Apply)   | Inline         | Next to the Apply button; table stays visible  |
| Timer refresh         | None           | Content updates in place; no loading indicator  |

**Behavior:** `_hasLoadedOnce` gates the initial block spinner. Once loaded, filter apply sets `_refreshing` and shows an inline spinner near the Apply button. The table remains visible with existing data. Background timer refreshes do not show any loading state and do not wipe content.

---

### Simulation Results Detail (`SimulationResults.razor`)

| Scenario              | Indicator Type | Placement                                      |
|-----------------------|----------------|------------------------------------------------|
| Initial load / BatchId change | Block    | Full-page until batch loads                    |
| Timer refresh         | None           | Content updates in place; no loading indicator  |

**Behavior:** When `_batch` is null and `_loading` is true, a block spinner is shown. When the timer fires (every 3 seconds for running batches), `LoadAsync` runs without setting `_loading`, so content is updated in place.

---

## 3. Error Handling Conventions

### Placement

- **Run Simulations:** Error appears directly below the "Start batch" button, near the action that caused it.
- **Batches:** Error appears above the filters section, near the Apply action.
- **Simulation Results:** Two error sources:
  1. Batch-level error (from API): Shown in the batch summary section.
  2. Rerun error (from "Rerun with same seed"): Shown near the rerun button.

### Display

- All errors use the `Alert` component with `Severity="AlertSeverity.Error"`.
- No toasts; errors are inline and persistent until the user retries or the error is cleared.
- Errors are cleared when the user retries the action (e.g., selecting a different event, clicking Apply again, or clicking Rerun).

### Technical

- Exceptions from service calls are caught and assigned to an error field.
- Background refresh (timer) does not surface errors to the UI to avoid disrupting the user; errors are only shown for user-initiated actions.

---

## 4. Technical Fixes Applied

### SimulationResults.razor

1. **Timer startup:** Timer is now started in `LoadAsync` after the batch is loaded and determined to be Running or Pending. Previously it was started in `OnAfterRender(firstRender)`, when `_batch` could still be null.
2. **Parameter race:** Removed `OnParametersSet` setting `_loading = true`. Loading is now driven by `OnParametersSetAsync` when `BatchId` changes. `_loadedBatchId` tracks which batch was loaded to avoid duplicate loads.
3. **Stale load guard:** `LoadAsync` captures `BatchId` at start and ignores results if `BatchId` has changed before the load completes.

### Batches.razor

1. **Filter apply vs refresh:** `LoadBatchesAsync(isBackgroundRefresh)` distinguishes user-initiated loads (show inline spinner, surface errors) from timer refreshes (no spinner, no error display, content stays visible).
2. **Initial vs subsequent load:** `_hasLoadedOnce` ensures the block spinner is shown only on first load; subsequent filter applies use the inline spinner.

---

## 5. Shared CSS (app.css)

The following styles were moved or added to `wwwroot/app.css` for consistency across simulation pages:

- `.page-header`, `.page-header h1` — Layout for page title and actions
- `.empty-state` — Centered empty state block
- `.table-container`, `.data-table` — Table layout and styling
- `.btn-outline` — Outline button variant
