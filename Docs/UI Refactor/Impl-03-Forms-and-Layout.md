# Implementation 03: Forms and Layout

**Slice:** Slice 3 — Forms, Guidance, and Layout  
**Date:** 2025-02-03

---

## 1. Form Changes

### FormField Component

**Location:** `BingoSim.Web/Components/Shared/FormField.razor`

**Parameters:**
- `Label` (string) — Field label
- `For` (string?) — Associated input id for accessibility
- `HelperText` (string?) — Optional explanatory text below the input
- `Tooltip` (string?) — Optional tooltip (via `title` on info icon)
- `ChildContent` (RenderFragment?) — The input or control

**Usage:** Wraps form inputs with consistent label, optional helper text, and optional tooltip icon (ⓘ).

### Run Simulations

| Field | Helper Text | Tooltip |
|-------|-------------|---------|
| Run count | Number of simulation runs per team (1–100,000). | — |
| Seed (optional) | Leave empty for random; use a fixed value for reproducible results. | — |
| Execution mode | — | Local: runs in-process on this server, good for quick tests. Distributed: uses worker processes (e.g. via RabbitMQ), good for large batches and horizontal scaling. |

**Grouping:** Event and Drafted teams in one section; Run count, Seed, Execution mode in a bordered "Simulation settings" section.

---

## 2. Layout Restructuring

### Simulation Results Detail

**Batch summary → Cards:**
- **Batch card:** ID, Runs requested, Seed, Status in a definition list
- **Progress card:** Completed, Failed, Running, Pending (and Retries if > 0) in a definition list; Elapsed and Runs/sec in a separate metrics block

**Progress bar:**
- Added percentage label above the bar: "X% complete"
- Bar and label wrapped in `.progress-bar-wrapper`

**Readability:** Definition lists (`<dl>`) with grid layout for aligned label/value pairs.

### Batches List

**Filters:**
- Wrapped in a card with "Filters" title
- Status dropdown and Event name search each have explicit labels
- Labels use `for`/`id` for accessibility

**Empty state:**
- Split into two lines
- Added CTA button: "Run Simulations" linking to `/simulations/run`

---

## 3. Behavior Unchanged

- **Run Simulations:** Event selection, team loading, run count, seed, execution mode, and Start batch behavior unchanged
- **Simulation Results:** Batch loading, progress display, refresh, rerun, aggregates, and sample timelines unchanged
- **Batches:** Filter logic, Apply, auto-refresh, and table display unchanged
- **Empty state:** CTA is an additional link; existing text preserved

---

## 4. Shared Styles Added (app.css)

- `.card`, `.card__title`, `.card__body` — Card layout for grouped content

---

## 5. Out of Scope (P2 Optional)

- Sample run timeline display improvement — not implemented (deferred)
