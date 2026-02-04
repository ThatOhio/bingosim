# Strategy Comparison Guide

This guide helps you choose between **Row Unlocking**, **Greedy**, and **ComboUnlocking** strategies when configuring teams for BingoSim simulations.

---

## Quick Reference

| Aspect | Row Unlocking | Greedy | ComboUnlocking |
|--------|---------------|--------|----------------|
| **Primary goal** | Unlock next row quickly | Maximize points immediately | Unlock rows + maximize activity reuse |
| **Task selection** | Optimal combination → furthest row → fallback | Highest points → fastest → key | Phase 1: Penalized combinations → fallback; Phase 2: Virtual score (points + shared bonus) |
| **Grant allocation** | Highest point on furthest row → fallback | Highest points → fastest → key | Phase 1: Same as RowUnlocking; Phase 2: Highest points anywhere |
| **Complexity** | Medium (caching) | Low (sort and pick) | High (two-phase, penalties, shared bonus) |
| **Predictability** | Depends on board structure | Simple, deterministic | Depends on activity overlap |

---

## When to Use ComboUnlocking

Choose **ComboUnlocking** when:

1. **Board has significant activity overlap** — The same activities appear on multiple rows. ComboUnlocking avoids "burning" activities early that you'll need for locked tiles.

2. **Long-term efficiency matters** — You want to maximize value from each activity. Completing a tile that helps progress multiple other tiles is more efficient.

3. **Row progression + activity reuse** — You care about unlocking rows and want to avoid activity conflicts that slow you down later.

4. **Team can handle strategic assignment** — Players may be assigned to lower-point tiles initially to preserve activities for locked rows.

---

## When to Use Row Unlocking

Choose **Row Unlocking** when:

1. **Goal is to reach later rows quickly** — You care more about progression through the board than raw point accumulation. Row unlocks may gate access to high-value tiles.

2. **Teams have varied capabilities** — The optimization logic considers which tile combinations unlock rows fastest. When players have different capability sets, the optimal path may differ.

3. **Board has efficient unlock combinations** — Some boards have low-point tiles that, when completed together, unlock rows faster than grinding high-point tiles. Row Unlocking finds these combinations.

4. **Long-term progression matters** — In multi-day or competitive events where "first to row N" is a milestone, Row Unlocking prioritizes that.

5. **High-point tiles are slow** — If 4-point tiles take much longer than 1–2 point tiles, Row Unlocking may unlock rows faster by completing cheaper tiles first.

---

## When to Use Greedy

Choose **Greedy** when:

1. **Goal is to maximize points in available time** — You want the highest possible score regardless of row progression.

2. **Simpler strategy is preferred** — Greedy is easier to understand and predict. No combination calculations; always pick highest points, then fastest.

3. **High-point tiles are relatively fast** — When 4-point tiles complete quickly, Greedy naturally accumulates points faster.

4. **Event is time-limited** — In short events where you may not unlock all rows, maximizing points per completed tile is more important.

5. **Debugging or baseline** — Greedy provides a straightforward baseline for comparison and is useful when validating simulation behavior.

---

## Trade-offs

### Row Unlocking

- **Pros:** May unlock rows faster; optimizes for progression; considers tile combinations.
- **Cons:** More complex; may sacrifice early points for faster unlocks; uses more memory (combination cache).

### Greedy

- **Pros:** Simple; predictable; typically faster per simulation; lower memory; maximizes immediate points.
- **Cons:** May unlock rows slower if high-point tiles are slow; does not optimize for row progression.

### ComboUnlocking

- **Pros:** May unlock rows faster while avoiding activity conflicts; maximizes activity reuse in Phase 2; best for boards with activity overlap.
- **Cons:** Most complex; highest memory usage; slower than Greedy; may deprioritize high-point tiles initially.

---

## Trade-off Matrix

| Strategy      | Complexity | Row Unlock Speed | Point Efficiency | Activity Reuse |
|---------------|------------|------------------|------------------|----------------|
| RowUnlocking  | Medium     | High             | Medium           | None           |
| Greedy        | Low        | Low              | High (short term)| None           |
| ComboUnlocking| High       | High             | High (long term) | Maximized      |

---

## Decision Flowchart

```
Does the board have significant activity overlap across rows?
├── Yes → Use ComboUnlocking (avoids burning activities, maximizes reuse)
└── No  → Do you care more about row progression or total points?
          ├── Row progression (unlock rows fast)
          │   └── Use Row Unlocking
          └── Total points (maximize score)
              └── Are high-point tiles fast to complete?
                  ├── Yes → Use Greedy
                  └── No  → Consider Row Unlocking (may unlock faster)
```

---

## API Usage

All strategies are registered in `StrategyCatalog` and `TeamStrategyFactory`:

```csharp
// Strategy keys
StrategyCatalog.RowUnlocking   // "RowUnlocking"
StrategyCatalog.Greedy        // "Greedy"
StrategyCatalog.ComboUnlocking // "ComboUnlocking"

// Get strategy from factory
var factory = new TeamStrategyFactory();
var strategy = factory.GetStrategy(StrategyCatalog.ComboUnlocking);
```

When creating or editing teams, set `StrategyKey` to one of these values. The UI dropdown lists all supported keys via `StrategyCatalog.GetSupportedKeys()`.

---

## Further Reading

- [Greedy Strategy Implementation](greedy-strategy-implementation.md) — Implementation details and code
- [Greedy Strategy Testing Results](greedy-strategy-testing-results.md) — Test results, benchmarks, and validation
- [ComboUnlocking Guide](combo-unlocking-guide.md) — When and how to use ComboUnlocking
- [ComboUnlocking Implementation](combo-unlocking-implementation.md) — Implementation details
- [ComboUnlocking Testing Results](combo-unlocking-testing-results.md) — Test results and benchmarks
