# Strategy Comparison Guide

This guide helps you choose between **Row Unlocking** and **Greedy** strategies when configuring teams for BingoSim simulations.

---

## Quick Reference

| Aspect | Row Unlocking | Greedy |
|--------|---------------|--------|
| **Primary goal** | Unlock next row as quickly as possible | Maximize points immediately |
| **Task selection** | Optimal combination for row unlock → furthest row → fallback | Highest points → fastest completion → key |
| **Grant allocation** | Highest point on furthest row → fallback | Highest points → fastest completion → key |
| **Complexity** | Higher (combination calculation, caching) | Lower (sort and pick) |
| **Predictability** | Depends on board structure | Simple, deterministic |

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

---

## Decision Flowchart

```
Do you care more about row progression or total points?
├── Row progression (unlock rows fast)
│   └── Use Row Unlocking
└── Total points (maximize score)
    └── Are high-point tiles fast to complete?
        ├── Yes → Use Greedy
        └── No  → Consider Row Unlocking (may unlock faster)
```

---

## API Usage

Both strategies are registered in `StrategyCatalog` and `TeamStrategyFactory`:

```csharp
// Strategy keys
StrategyCatalog.RowUnlocking  // "RowUnlocking"
StrategyCatalog.Greedy       // "Greedy"

// Get strategy from factory
var factory = new TeamStrategyFactory();
var strategy = factory.GetStrategy(StrategyCatalog.Greedy);
```

When creating or editing teams, set `StrategyKey` to one of these values. The UI dropdown lists all supported keys via `StrategyCatalog.GetSupportedKeys()`.

---

## Further Reading

- [Greedy Strategy Implementation](greedy-strategy-implementation.md) — Implementation details and code
- [Greedy Strategy Testing Results](greedy-strategy-testing-results.md) — Test results, benchmarks, and validation
