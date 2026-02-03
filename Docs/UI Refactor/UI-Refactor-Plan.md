# UI Refactor Plan: Run Simulations & Simulation Results

**Scope:** Run Simulations page (`/simulations/run`), Simulation Results list (`/simulations/results`), Simulation Results detail (`/simulations/results/{BatchId}`)  
**Stack:** Blazor Interactive Server  
**Goal:** Usability-first improvements without changing functionality.

---

## 1. Current Pain Points

### Run Simulations (`RunSimulations.razor`)

| Area | Pain Point |
|------|------------|
| **Feedback** | Loading message "Loading events…" appears at bottom of markup; no loading state when switching events (teams load silently). |
| **Feedback** | Error shown as plain `<p class="text-danger">` with no icon or card treatment; easy to miss. |
| **Feedback** | No success feedback before redirect; user may not realize batch started successfully. |
| **Forms** | Run count input has no helper text (valid range 1–100,000); users may not know limits. |
| **Forms** | Seed input placeholder is minimal; no explanation of reproducibility. |
| **Forms** | Execution mode (Local vs Distributed) has no explanation; users may not know when to choose which. |
| **Layout** | `page-header` used but not defined in `RunSimulations.razor.css`; layout may be inconsistent with other pages. |
| **Layout** | Form sections lack visual grouping; execution mode radios feel disconnected from other fields. |
| **Technical** | `OnEventSelectedAsync` runs on every bind; no debounce or loading indicator for team fetch. |

### Simulation Results List (`Batches.razor`)

| Area | Pain Point |
|------|------------|
| **Feedback** | "Loading…" is plain text; no skeleton or spinner. |
| **Feedback** | No feedback when filters are applied; user may not know if list updated. |
| **Auto-refresh** | "Refreshing every 5 seconds…" is passive; no last-updated timestamp, no manual refresh, no pause. |
| **Layout** | Filters row (`batches-filters`) has no label or grouping; "Apply" button purpose may be unclear. |
| **Layout** | Empty state is minimal; could guide user to Run Simulations. |
| **Technical** | Timer starts on first load with active batches; `_loading` is set true on every `LoadBatchesAsync`, causing full re-render. |

### Simulation Results Detail (`SimulationResults.razor`)

| Area | Pain Point |
|------|------------|
| **Feedback** | "Loading…" only when batch is null; no loading state when refreshing in-place. |
| **Feedback** | Batch summary is a dense single-line paragraph; hard to scan. |
| **Feedback** | Progress section is text-heavy; metrics and counts run together. |
| **Feedback** | Error and rerun error shown as plain text; no visual hierarchy. |
| **Auto-refresh** | "Refreshing every 3 seconds…" with no last-updated, no manual refresh, no pause. |
| **Layout** | Progress bar has no percentage label; user must infer from bar width. |
| **Layout** | Sample run timelines use raw JSON in `<pre>`; hard to read. |
| **Technical** | Timer started in `OnAfterRender` with `firstRender`; `_batch` may still be null, so timer may not start. |
| **Technical** | `OnParametersSet` sets `_loading = true` but `OnParametersSetAsync` calls `LoadAsync`; potential race. |

---

## 2. Prioritized Improvements

### P0 — Critical (blocks usability)

| ID | Improvement | Page(s) |
|----|-------------|---------|
| P0-1 | Show loading state when fetching teams on event change | Run Simulations |
| P0-2 | Show loading state when applying filters | Batches |
| P0-3 | Ensure timer starts correctly when batch is running (fix `OnAfterRender` logic) | Simulation Results detail |
| P0-4 | Add last-updated timestamp for auto-refresh pages | Batches, Simulation Results detail |
| P0-5 | Add manual refresh button | Batches, Simulation Results detail |

### P1 — High (significant usability gain)

| ID | Improvement | Page(s) |
|----|-------------|---------|
| P1-1 | Add helper text for run count (1–100,000), seed, execution mode | Run Simulations |
| P1-2 | Add tooltip or inline explanation for Local vs Distributed | Run Simulations |
| P1-3 | Improve error display (card/alert treatment, icon) | All three pages |
| P1-4 | Add pause/resume for auto-refresh | Batches, Simulation Results detail |
| P1-5 | Restructure batch summary and progress into scannable cards/sections | Simulation Results detail |
| P1-6 | Add progress percentage label next to progress bar | Simulation Results detail |
| P1-7 | Consistent `page-header` styling across simulation pages | All three |

### P2 — Nice to have

| ID | Improvement | Page(s) |
|----|-------------|---------|
| P2-1 | Skeleton or spinner for initial load | All three |
| P2-2 | Top-of-page quick-start guide for Run Simulations | Run Simulations |
| P2-3 | Improve sample run timeline display (formatted JSON or structured view) | Simulation Results detail |
| P2-4 | Empty state CTA to Run Simulations | Batches |
| P2-5 | Debounce or loading indicator for event change | Run Simulations |

---

## 3. Proposed Reusable UI Primitives / Patterns

### 3.1 Shared Components

| Component | Purpose | Props |
|-----------|---------|-------|
| `LoadingSpinner` | Small inline or block spinner | `Size`, `Label` |
| `Alert` | Error/success/warning/info message | `Severity`, `Message`, `Dismissible` |
| `RefreshIndicator` | Last-updated + manual refresh + pause | `LastUpdated`, `IsPaused`, `OnRefresh`, `OnPauseToggle`, `IntervalSeconds` |
| `FormField` | Label + input + optional helper text | `Label`, `For`, `HelperText`, `ChildContent` |
| `PageHeader` | Title + actions (consistent layout) | `Title`, `ChildContent` (actions) |
| `Card` | Grouped content with optional title | `Title`, `ChildContent` |

### 3.2 Shared CSS Classes (in `app.css` or shared stylesheet)

| Class | Purpose |
|-------|---------|
| `.page-header` | Flex layout for title + actions |
| `.form-section` | Max-width, spacing for form blocks |
| `.form-group` | Label + field spacing |
| `.alert`, `.alert-danger`, `.alert-success` | Consistent alert styling |
| `.refresh-indicator` | Layout for last-updated + buttons |
| `.card`, `.card-body` | Grouped content blocks |

### 3.3 Patterns

- **Loading states:** Use `LoadingSpinner` or skeleton; avoid plain "Loading…" text only.
- **Errors:** Use `Alert` with severity; place near relevant action (e.g., form, refresh).
- **Auto-refresh:** Always show last-updated, manual refresh, and pause; use `RefreshIndicator`.
- **Forms:** Use `FormField` with helper text for non-obvious inputs.
- **Dense data:** Use `Card` to group batch summary, progress, aggregates.

---

## 4. Phased Implementation Plan

### Slice 1: Feedback and Status (P0 + P1 error/loading)

**Goal:** Reliable loading and error feedback across all three pages.

**Tasks:**
1. Add `LoadingSpinner` and `Alert` shared components.
2. Run Simulations: Show loading when fetching teams; use `Alert` for errors.
3. Batches: Show loading when applying filters; use `Alert` for errors.
4. Simulation Results detail: Fix timer startup; use `Alert` for errors.
5. Move `page-header` (and related) to shared `app.css` or a shared layout CSS so simulation pages render consistently.

**Deliverables:** Loading states and error display improved; timer logic fixed.

---

### Slice 2: Auto-Refresh UX (P0-4, P0-5, P1-4)

**Goal:** Transparent, controllable auto-refresh.

**Tasks:**
1. Add `RefreshIndicator` component (last-updated, manual refresh, pause).
2. Batches: Integrate `RefreshIndicator`; add `LastUpdated` state; support pause.
3. Simulation Results detail: Integrate `RefreshIndicator`; add `LastUpdated`; support pause.
4. Ensure manual refresh does not reset `_loading` in a way that hides content (consider partial loading or overlay).

**Deliverables:** Users see when data was last updated; can refresh manually or pause auto-refresh.

---

### Slice 3: Forms, Guidance, and Layout (P1-1, P1-2, P1-5, P1-6, P2)

**Goal:** Clearer forms and information hierarchy.

**Tasks:**
1. Add `FormField` (or equivalent) with helper text support.
2. Run Simulations: Add helper text for run count, seed, execution mode; add tooltip for Local vs Distributed.
3. Simulation Results detail: Restructure batch summary and progress into cards; add progress percentage.
4. Batches: Improve filter section labeling; enhance empty state (P2-4).
5. (Optional) Improve sample run timeline display (P2-3).

**Deliverables:** Forms are self-explanatory; batch detail is easier to scan.

---

## 5. Testing Considerations (bUnit Focus Areas)

**Note:** `BingoSim.Web.Tests` currently has no bUnit package. Add `bunit` and `bunit.web` for component tests.

### Run Simulations

| Scenario | Focus |
|----------|-------|
| Initial load | Events load; loading state shown then hidden |
| Event change | Teams load; loading state shown when switching events |
| Start batch (success) | Button disabled during start; navigation occurs |
| Start batch (error) | Error message displayed via `Alert` |
| Validation | Run count bounds; seed optional |

### Batches (Simulation Results List)

| Scenario | Focus |
|----------|-------|
| Initial load | Loading state; table or empty state |
| Filter apply | Loading state; list updates |
| Auto-refresh | Timer behavior; `LastUpdated` updates |
| Pause/resume | Pause stops timer; resume restarts |

### Simulation Results Detail

| Scenario | Focus |
|----------|-------|
| Batch not found | "Batch not found" message |
| Batch loading | Loading state |
| Batch running | Timer starts; progress bar and metrics update |
| Manual refresh | `LoadAsync` invoked; UI updates |
| Pause | Timer stops; no further auto-refresh until resume |
| Rerun with same seed | Navigation to new batch |

### Shared Components

| Component | Focus |
|-----------|-------|
| `LoadingSpinner` | Renders when visible; respects `Label` |
| `Alert` | Renders message; severity affects styling |
| `RefreshIndicator` | Last-updated, refresh button, pause toggle callbacks |

---

## 6. Out of Scope (Explicitly Excluded)

- Changing navigation flow or URLs
- Adding new features (e.g., batch cancellation, export)
- Visual redesign (colors, typography overhaul)
- Changing backend APIs or DTOs
- Modifying simulation execution logic

---

## 7. References

- `BingoSim.Web/Components/Pages/Simulations/RunSimulations.razor`
- `BingoSim.Web/Components/Pages/Simulations/Batches.razor`
- `BingoSim.Web/Components/Pages/Simulations/SimulationResults.razor`
- `BingoSim.Web/Components/Pages/Events/Events.razor` (page-header, table, empty-state patterns)
- `Docs/Simulation_Strategies_Explained.md` (for execution mode / strategy context)
