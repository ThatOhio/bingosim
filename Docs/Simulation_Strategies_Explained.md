# Simulation Strategies: How They Work (Code-Based)

This document explains the simulation strategy from the perspective of an individual player, based on the actual implementation in the codebase.

---

## Overview: How the Simulation Models a Player

The simulation is a **discrete-event model**. Time advances as events occur: each event is "one or more players finish an activity attempt." When you (as a simulated player) are online, the system:

1. **Assigns you to work** on a tile using the team's strategy (Row Unlocking)
2. **Schedules an attempt** — you start the activity, and an end time is computed (duration is sampled from the activity definition, modified by your skill and group size)
3. **When the attempt completes**, the activity rolls for outcomes; if you get a "grant" (progress), it is allocated to a tile
4. **You are re-scheduled** for your next attempt

You only play when you're within your **schedule windows** (e.g., Tuesday 18:00–22:00). When the event queue is empty and no one is online, time fast-forwards to the next session start.

---

## Row Unlocking (Shared Mechanics)

Rows unlock sequentially. **Row 0** is always available at the start. **Row N** unlocks when the team has completed enough points in row N-1 (the `UnlockPointsRequiredPerRow` threshold). You cannot work on or receive progress for tiles in a locked row.

---

## Task Assignment: What You Choose to Do

**Task assignment is strategy-driven.** The Row Unlocking strategy (`RowUnlockingStrategy`) selects which tile you work on:

1. **Optimal combination** — For the furthest unlocked row, the strategy computes all tile combinations that meet the unlock threshold (e.g., 5 points).
2. **Shortest time** — It picks the combination with the shortest estimated completion time.
3. **Highest points first** — Within that combination, you are assigned to the highest-point tile you can work on (considering your capabilities).
4. **Fallback** — If the optimal combination is complete, you work on the highest-point tile on the furthest row, then any unlocked row.

So you work on tiles that **unlock the next row fastest**, preferring high-value tiles within that set.

---

## Progress Allocation: Where Your Grant Goes

When an activity attempt succeeds, it produces **grants** — each with a `DropKey` (e.g. `"drop"`, `"drop.rare"`) and `Units` of progress. A tile accepts a grant if it lists that `DropKey` in its `AcceptedDropKeys`.

**Often, multiple tiles can accept the same DropKey.** The **strategy** decides which eligible tile receives the grant.

**Row Unlocking grant allocation:**
1. **Primary:** Highest-point tile on the **furthest unlocked row** that accepts the grant.
2. **Fallback:** Highest-point tile from all eligible tiles.
3. **Tie-break:** Tile key (alphabetical) for determinism.

---

## Strategy: Row Unlocking

**Code:** `RowUnlockingStrategy` in `BingoSim.Application/Simulation/Strategies/RowUnlockingStrategy.cs`

**Task selection:** Optimal combination for unlocking the next row (shortest estimated time), then highest-point tiles within that set. Fallback to highest-point tile on furthest row, then any unlocked row.

**Grant allocation:** Highest-point tile on furthest unlocked row, then highest-point tile anywhere.

**From your perspective:** You work on tiles that unlock the next row as quickly as possible. Progress is routed to high-value tiles on the furthest row to maximize row progression.

---

## When the Strategy Matters

The strategy affects behavior when **multiple tiles are eligible** for the same grant or when **multiple tiles** could be your next task. Row Unlocking optimizes for the combination that unlocks the next row fastest.

---

## Higher-Level Concepts

| Concept | Description |
|--------|-------------|
| **Team-level strategy** | The strategy is configured per team. All players on a team use the same strategy. |
| **Shared progress** | Progress is tracked per team, not per player. When you complete a tile, it contributes to the team's row unlock and total points. |
| **Deterministic tie-break** | When points are equal, the strategy uses tile key order (e.g. `"a"` before `"x"`) so results are reproducible. |
| **Task assignment vs allocation** | What you *choose to do* (task assignment) and what tile *receives your progress* (allocation) are both strategy-driven in Row Unlocking. |

---

## Summary

- **RowUnlocking**: Optimizes for unlocking the next row as quickly as possible. Task selection uses optimal tile combinations; grant allocation focuses on highest-point tiles on the furthest unlocked row.
