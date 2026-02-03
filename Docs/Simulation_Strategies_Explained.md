# Simulation Strategies: How They Work (Code-Based)

This document explains the two simulation strategies from the perspective of an individual player, based on the actual implementation in the codebase.

---

## Overview: How the Simulation Models a Player

The simulation is a **discrete-event model**. Time advances as events occur: each event is "one or more players finish an activity attempt." When you (as a simulated player) are online, the system:

1. **Assigns you to work** on a tile by picking the first incomplete tile you're eligible for
2. **Schedules an attempt** — you start the activity, and an end time is computed (duration is sampled from the activity definition, modified by your skill and group size)
3. **When the attempt completes**, the activity rolls for outcomes; if you get a "grant" (progress), it is allocated to a tile
4. **You are re-scheduled** for your next attempt

You only play when you're within your **schedule windows** (e.g., Tuesday 18:00–22:00). When the event queue is empty and no one is online, time fast-forwards to the next session start.

---

## Row Unlocking (Shared by Both Strategies)

Rows unlock sequentially. **Row 0** is always available at the start. **Row N** unlocks when the team has completed enough points in row N-1 (the `UnlockPointsRequiredPerRow` threshold). You cannot work on or receive progress for tiles in a locked row.

---

## Task Assignment: What You Choose to Do

**Task assignment is the same for both strategies.** The logic lives in `GetFirstEligibleActivity` in `SimulationRunner.cs`:

- Rows are iterated in order (0, 1, 2, …)
- Within each row, tiles are ordered by points ascending (1, 2, 3, 4)
- The first incomplete tile you're eligible for (you meet its requirements, it's in an unlocked row) is chosen

So you always work on the **first incomplete tile in the first unlocked row**, preferring lower-point tiles first. This is row-rush-like in spirit: you fill rows from top to bottom, and within a row you tend toward easier (lower-point) tiles first.

---

## Progress Allocation: Where Your Grant Goes

When an activity attempt succeeds, it produces **grants** — each with a `DropKey` (e.g. `"drop"`, `"drop.rare"`) and `Units` of progress. A tile accepts a grant if it lists that `DropKey` in its `AcceptedDropKeys`.

**Often, multiple tiles can accept the same DropKey.** For example, a generic "kill boss" activity might produce `"drop"`, and several tiles in different rows might all accept `"drop"`. The **strategy** decides which eligible tile receives the grant.

The strategy is implemented as an `IProgressAllocator` that receives an `AllocatorContext` (eligible tiles, row indices, points, progress, etc.) and returns the tile key that should get the grant.

---

## Strategy 1: RowRush

**Code:** `RowRushAllocator` in `BingoSim.Application/Simulation/Allocation/RowRushAllocator.cs`

**Selection order:**
1. Lowest row index
2. Then lowest points (1, then 2, then 3, then 4)
3. Tie-break: tile key (alphabetical, deterministic)

**From your perspective:** When you earn progress and more than one tile could take it, the team routes it to the tile that is **earliest in the row order** and **lowest in points**. You effectively complete rows in order and prefer cheaper tiles within each row. This keeps the board filling from top to bottom and unlocks new rows as soon as possible.

---

## Strategy 2: GreedyPoints

**Code:** `GreedyPointsAllocator` in `BingoSim.Application/Simulation/Allocation/GreedyPointsAllocator.cs`

**Selection order:**
1. Highest points (4, then 3, then 2, then 1)
2. Then lowest row index
3. Tie-break: tile key (alphabetical, deterministic)

**From your perspective:** When you earn progress and more than one tile could take it, the team routes it to the tile with the **highest point value** first, then the earliest row. You prioritize high-value tiles to maximize total points, even if that means leaving lower-point tiles in earlier rows for later.

---

## When the Strategy Matters

The strategy only affects behavior when **multiple tiles are eligible** for the same grant. That happens when:

- Several tiles share the same `AcceptedDropKeys` (e.g. all accept `"drop"`)
- All those tiles are in unlocked rows
- None of them are already completed

If only one tile can accept the grant, both strategies behave the same. The allocator is invoked for every grant; when there are multiple eligible tiles, RowRush and GreedyPoints choose differently.

---

## Higher-Level Concepts

| Concept | Description |
|--------|-------------|
| **Team-level strategy** | The strategy is configured per team. All players on a team use the same allocator when their grants are routed. |
| **Shared progress** | Progress is tracked per team, not per player. When you complete a tile, it contributes to the team's row unlock and total points. |
| **Deterministic tie-break** | When row and points are equal, both strategies use tile key order (e.g. `"a"` before `"x"`) so results are reproducible. |
| **Task assignment vs allocation** | What you *choose to do* (task assignment) is fixed. What tile *receives your progress* when multiple could (allocation) is strategy-dependent. |

---

## Summary

- **RowRush**: Complete rows in order; within a row, prefer lower-point tiles. Optimizes for unlocking rows quickly.
- **GreedyPoints**: Prefer highest-point tiles first, then earlier rows. Optimizes for total points.

Both strategies use the same task-assignment logic (first incomplete tile, row order, then points ascending). The only difference is how progress is routed when multiple tiles can accept the same grant.
