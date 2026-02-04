# ComboUnlocking Strategy Guide

This guide explains when and how to use the ComboUnlocking strategy, with examples and recommendations.

---

## Overview

ComboUnlocking is a two-phase strategy that:

1. **Phase 1 (Row Unlocking):** Unlocks rows while avoiding activities needed for locked tiles
2. **Phase 2 (Shared Activity Maximization):** Prioritizes tiles that share activities with the most other incomplete tiles

It is the most sophisticated strategy, designed for boards with significant activity overlap across rows.

---

## When to Use ComboUnlocking

Choose **ComboUnlocking** when:

1. **Board has significant activity overlap** — The same activities appear on multiple rows. ComboUnlocking avoids "burning" activities early that you'll need later.

2. **Long-term efficiency matters** — You want to maximize value from each activity run. Completing a tile that helps progress multiple other tiles is more efficient.

3. **Row progression is important** — You care about unlocking rows, but also want to avoid activity conflicts that slow you down later.

4. **Team can handle strategic assignment** — Players may be assigned to lower-point tiles initially to preserve activities for locked rows.

---

## When NOT to Use ComboUnlocking

- **Minimal activity overlap** — If each tile has unique activities, ComboUnlocking behaves like RowUnlocking in Phase 1 and Greedy in Phase 2. Use the simpler strategy instead.

- **Time-limited events** — In very short events where you won't unlock all rows, Greedy may be simpler and sufficient.

- **Debugging or baseline** — Use Greedy or RowUnlocking for straightforward behavior when validating simulations.

---

## Phase 1: Penalty Calculation

### How It Works

For each tile in a combination that unlocks the next row:

```
penalizedTime = baseTime × (1 + lockedShareCount)
```

Where `lockedShareCount` = number of locked tiles (on rows not yet unlocked) that share at least one activity with this tile.

### Example

- **Row 0 (unlocked):** Tile A (Activity1, 2pts), Tile B (Activity2, 3pts), Tile C (Activity3, 4pts), Tile D (Activity1, 1pt)
- **Row 1 (locked):** Tiles with Activity1, Activity2
- **Row 2 (locked):** Tiles with Activity1

Tile C uses Activity3, which appears only on Row 0. So C has 0 locked shares → penalty = baseTime × 1.

Tiles A and D use Activity1, which appears on Rows 1 and 2. So each has 2 locked shares → penalty = baseTime × 3.

The combination [C, D] has lower total penalized time than [A, B]. ComboUnlocking selects [C, D] and assigns the player to the highest-point tile in that combination (C).

---

## Phase 2: Shared Activity Bonus

### How It Works

After all rows are unlocked:

```
virtualScore = points + (1 × sharedIncompleteTileCount)
```

Where `sharedIncompleteTileCount` = number of other incomplete tiles that share at least one activity with this tile.

Sort by: virtual score (desc) → completion time (asc) → tile key (asc).

### Example

- **Tile A (2pts):** Shares Activity1 with 3 other incomplete tiles → virtual score = 2 + 3 = 5
- **Tile B (4pts):** Unique activity, 0 shares → virtual score = 4 + 0 = 4

ComboUnlocking selects Tile A first, because completing it helps progress three other tiles.

---

## Phase Transition

The strategy switches from Phase 1 to Phase 2 when:

```csharp
unlockedRowIndices.Count >= snapshot.Rows.Count
```

- **Single-row event:** Phase 2 from the start
- **Multi-row event:** Phase 1 until the last row unlocks, then Phase 2

---

## Performance Characteristics

- **Complexity:** Highest of the three strategies
- **Caching:** Uses combination cache (like RowUnlocking) plus penalized combination cache
- **Cache invalidation:** Cleared when rows unlock (wired in SimulationRunner)
- **Typical throughput:** Comparable to RowUnlocking; slower than Greedy

---

## API Usage

```csharp
// Strategy key
StrategyCatalog.ComboUnlocking  // "ComboUnlocking"

// Get from factory
var factory = new TeamStrategyFactory();
var strategy = factory.GetStrategy(StrategyCatalog.ComboUnlocking);
```

---

## Further Reading

- [ComboUnlocking Implementation](combo-unlocking-implementation.md) — Implementation details
- [ComboUnlocking Testing Results](combo-unlocking-testing-results.md) — Test results and benchmarks
- [Strategy Comparison Guide](strategy-comparison-guide.md) — When to use each strategy
