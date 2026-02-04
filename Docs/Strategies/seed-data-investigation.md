# Seed Data Investigation: RowUnlocking Row 8 Issue

## Executive Summary

Investigation of seed data configuration to determine why **RowUnlocking** stops at row 8 (avg 54.7 points, 19.5 tiles) while **ComboUnlocking** reaches row 19 (avg 166.8 points, 57.5 tiles) in E2E tests. Strategy logic is proven correct in controlled tests—both reach row 19 with identical snapshots. The 100% failure rate for RowUnlocking with seed data suggests a **systematic configuration issue**.

**Key finding:** Row 8 in the seed data requires **group-only activities (CoX or ToA)** to reach the 5-point unlock threshold. Combined with **limited player schedules** and **team composition differences**, Team Alpha (RowUnlocking) may have insufficient schedule overlap for group activities, while Team Beta (ComboUnlocking) may have better overlap.

---

## 1. Seed Data Configuration

### 1.1 Seed Data Location

| Component | Location |
|-----------|----------|
| Main seed service | `BingoSim.Application/Services/DevSeedService.cs` |
| Seed program | `BingoSim.Seed/Program.cs` |
| Documentation | `Docs/DEV_SEEDING.md` |

### 1.2 Event Structure (Winter Bingo 2025 / Spring League Bingo)

| Property | Value |
|----------|-------|
| Event Name | Winter Bingo 2025, Spring League Bingo |
| Total Rows | 20 (indices 0–19) |
| Duration | Winter: 24 hours (86,400s), Spring: 48 hours (172,800s) |
| UnlockPointsRequiredPerRow | 5 |
| Tiles per row | 4 (points 1, 2, 3, 4) |
| Total points per row | 10 (1+2+3+4) |

### 1.3 Row Generation Algorithm

`BuildEventRows` uses a deterministic pattern:

- **Activity assignment:** `activityIdx = (rowIdx + tilePos) % 6`
- **Activities (indices 0–5):** boss.zulrah, boss.vorkath, skilling.runecraft, skilling.mining, raid.cox, raid.toa
- **Combo tiles:** `rowIdx % 7 == 2 && tilePos == 2` (rows 2, 9, 16)
- **Rare-only tiles:** `rowIdx % 5 == 1 && tilePos == 3` (rows 1, 6, 11, 16)

### 1.4 Row 8 Specific Configuration

| Tile | Key | Points | Activity | RequiredCount | Requirements |
|------|-----|--------|----------|---------------|--------------|
| 0 | t1-r8 / s1-r8 | 1 | skilling.runecraft | 1 | None |
| 1 | t2-r8 / s2-r8 | 2 | skilling.mining | 1 | None |
| 2 | t3-r8 / s3-r8 | 3 | raid.cox | 1 | quest.sote |
| 3 | t4-r8 / s4-r8 | 4 | raid.toa | 1 | quest.sote |

**Row 8 total points available:** 10 (1+2+3+4)

**Points required to unlock row 9:** 5

**Valid combinations for 5 points:**
- 2 + 3 = Mining (2pt) + CoX (3pt)
- 1 + 4 = Runecraft (1pt) + ToA (4pt)

**Critical:** Both paths require either **raid.cox** or **raid.toa**. These activities have:
- `SupportsSolo = false`, `SupportsGroup = true`
- **Minimum 2 players** must be assigned and online simultaneously to run them

---

## 2. Activity Definitions (Seed)

| Activity | Baseline Time | Variance | SupportsSolo | SupportsGroup |
|----------|--------------|----------|--------------|---------------|
| boss.zulrah | 90–120s | 20–30s | ✓ | ✓ |
| boss.vorkath | 180s | 45s | ✓ | ✗ |
| skilling.runecraft | 60s | 10s | ✓ | ✗ |
| skilling.mining | 45s | 15s | ✓ | ✓ |
| raid.cox | 600–900s | 120–180s | ✗ | ✓ |
| raid.toa | 1800s | 300s | ✗ | ✓ |

**Row 8 time estimates (TileCompletionEstimator):**
- Runecraft (1pt): ~60s
- Mining (2pt): ~45s
- CoX (3pt): ~900s (uses max attempt)
- ToA (4pt): ~1800s

**Optimal combination for RowUnlocking:** Mining + CoX ≈ 945s (vs Runecraft + ToA ≈ 1860s)

---

## 3. Activity Overlap Analysis

### 3.1 Row 8 Activities vs Rows 9–19

| Row 8 Activity | Locked Rows (9–19) with Same Activity |
|-----------------|--------------------------------------|
| skilling.runecraft | 11, 17 |
| skilling.mining | 9, 15 |
| raid.cox | 9, 10, 13, 16, 19 |
| raid.toa | 9, 10, 15, 16 |

### 3.2 ComboUnlocking Penalty Impact

ComboUnlocking applies: `penalizedTime = baseTime × (1 + lockedShareCount)`.

For row 8 combinations (when rows 9–19 are locked):
- **Mining:** 2 locked tiles share → penalty factor 3
- **CoX:** 5 locked tiles share → penalty factor 6
- **Runecraft:** 2 locked tiles share → penalty factor 3
- **ToA:** 4 locked tiles share → penalty factor 5

**Combination [Mining, CoX]:** 45×3 + 900×6 = 5535s penalized  
**Combination [Runecraft, ToA]:** 60×3 + 1800×5 = 9180s penalized

Both strategies would still choose Mining + CoX (lowest penalized time). **Activity overlap alone does not explain different behavior on row 8.**

---

## 4. Team Configurations

### 4.1 Current Seed Configuration

**Important:** Both teams use **RowUnlocking** in the seed. To compare RowUnlocking vs ComboUnlocking, one team must be changed to ComboUnlocking (e.g., via UI or test setup).

| Team | Strategy (Seed) | Players | Player Names |
|------|-----------------|---------|--------------|
| Team Alpha | RowUnlocking | 4 | Alice, Bob, Carol, Dave |
| Team Beta | RowUnlocking | 4 | Eve, Frank, Grace, Henry |

### 4.2 Player Capabilities

| Player | Capabilities | Can do CoX/ToA (quest.sote)? |
|--------|--------------|------------------------------|
| Alice | quest.ds2 | ✗ |
| Bob | item.dragon_hunter_lance, quest.ds2 | ✗ |
| Carol | quest.sote | ✓ |
| Dave | item.dragon_hunter_lance, quest.sote | ✓ |
| Eve | (none) | ✗ |
| Frank | quest.ds2, item.dragon_hunter_lance | ✗ |
| Grace | quest.sote | ✓ |
| Henry | quest.ds2, quest.sote | ✓ |

**Both teams have 2 players with quest.sote** (Alpha: Carol, Dave; Beta: Grace, Henry). Capability mismatch is **not** the cause.

### 4.3 Player Schedules (Weekly)

| Player | Schedule (Day, Start, Duration) |
|--------|--------------------------------|
| Alice | Mon 18:00 2h, Wed 19:00 1.5h, Fri 20:00 3h |
| Bob | Tue 17:00 1h, Tue 21:00 1.5h, Thu 18:00 2h, Sat 10:00 4h |
| Carol | Mon 12:00 0.75h, Mon 18:00 1.5h, Wed 18:00 1h, Sun 14:00 3h |
| Dave | Tue 19:00 2h, Thu 19:00 2h, Sat 09:00 1.5h, Sat 15:00 1.5h |
| Eve | Mon 20:00 1h, Wed 20:00 1h, Fri 20:00 1.5h, Sun 10:00 2h |
| Frank | Tue 17:30 2.5h, Thu 17:30 2.5h, Sat 08:00 3h, Sat 14:00 2h |
| Grace | Mon 19:00 1.5h, Fri 19:00 2h, Sun 12:00 4h |
| Henry | Tue 18:00 1.5h, Wed 18:00 1.5h, Thu 18:00 1.5h, Sat 10:00 3h |

**Schedule overlap for group activities (2+ players online):**
- **Team Alpha:** Carol and Dave both have quest.sote. Overlap depends on event start time. Carol: Mon 18:00, Wed 18:00, Sun 14:00. Dave: Tue 19:00, Thu 19:00, Sat. Limited overlap.
- **Team Beta:** Grace and Henry both have quest.sote. Henry: Tue/Wed/Thu 18:00, Sat 10:00. Grace: Mon/Fri 19:00, Sun 12:00. Different pattern—Henry has more weekday evenings; Grace has longer weekend blocks.

**Hypothesis:** Team Beta may have more favorable schedule overlap for 2+ players (needed for CoX/ToA) depending on event start day/time. If the event starts on a day when Alpha has poor overlap but Beta has good overlap, RowUnlocking (Alpha) could stall at row 8 while ComboUnlocking (Beta) progresses.

---

## 5. Root Cause Analysis

### 5.1 Most Likely Cause: Group Activity + Schedule Overlap

**Row 8 requires CoX or ToA** to reach 5 points. Both are **group-only** (2+ players). If Team Alpha’s schedule rarely has 2+ quest.sote players online together during the event window, it cannot complete CoX/ToA and cannot unlock row 9.

**Why ComboUnlocking (Beta) might succeed:**
1. **Different schedule:** Team Beta’s Grace and Henry may have more overlapping online time.
2. **Different test setup:** If the comparison uses different team compositions (e.g., Beta with always-online players), Beta would not be constrained by schedules.

### 5.2 Ruled Out

| Hypothesis | Verdict |
|------------|---------|
| Insufficient points on row 8 | Row 8 has 10 pts; 5 pts required. ✓ |
| Capability mismatch | Both teams have 2 players with quest.sote. ✓ |
| Activity overlap exhausts activities | Activities are reusable; no exhaustion. ✓ |
| Strategy logic bug | Both strategies reach row 19 in controlled tests. ✓ |

### 5.3 Event Duration

- Winter: 86,400s (24h)
- Spring: 172,800s (48h)

Estimated time to complete row 8 (Mining + CoX): ~945s. Cumulative time through row 8 depends on earlier rows (CoX/ToA on other rows add significant time). Duration is unlikely to be the primary cause unless schedule overlap is very poor.

---

## 6. Reproduction Test

### 6.1 Minimal Reproduction (Seed-Like Configuration)

**Test location:** `Tests/BingoSim.Application.UnitTests/Simulation/SeedDataRow8ReproductionTests.cs`

The test `SeedLikeRow8_AlwaysOnlineSoloOnly_BothStrategiesReachRow8` verifies that with solo-only activities (no group constraint), both strategies reach row 8. This establishes the baseline.

To reproduce the group-activity constraint, use a snapshot with:
- Row 8: Runecraft(1pt), Mining(2pt), CoX(3pt, quest.sote), ToA(4pt, quest.sote)
- CoX/ToA: `SupportsSolo=false`, require 2+ players
- **Critical:** The seed uses **4 players per team**. With 2+3 or 1+4 combinations, you need 2 on group + 1 on solo = 3 players minimum. With only 2 players, the strategy may assign 1 to group (stuck) and 1 to solo. Use 4 players to match seed.

### 6.2 Expected vs Actual

| Scenario | Expected | Actual (if issue reproduces) |
|----------|----------|-------------------------------|
| Alpha (RowUnlocking), limited overlap | Stops at row 8 | RowReached=8, ~54.7 pts |
| Beta (ComboUnlocking), better overlap | Reaches row 19 | RowReached=19, ~166.8 pts |

---

## 7. Proposed Fixes

### 7.1 Option A: Fix Seed Data (Recommended)

1. **Ensure both teams use ComboUnlocking for comparison tests**  
   Update `SeedTeamsAsync` so Team Beta uses ComboUnlocking when running strategy comparison E2E tests.

2. **Align schedule overlap across teams**  
   Give Alpha and Beta similar weekly schedules so group activity availability is comparable. For example, ensure both have 2+ quest.sote players online for at least 2–3 hours on the same days.

3. **Add a “strategy comparison” seed variant**  
   A dedicated seed that creates Alpha (RowUnlocking) and Beta (ComboUnlocking) with **identical** player capabilities and **identical** schedules (e.g., always-online or mirrored schedules) to isolate strategy behavior from schedule effects.

### 7.2 Option B: Strategy Logic (If Seed Is Correct)

If the intent is that RowUnlocking should perform well even with limited schedule overlap:

1. **Fallback when group activities are blocked**  
   When the optimal combination requires group activities but no group is available, consider fallback combinations that use only solo activities (e.g., 1+2+2 if such a combination existed; currently 1+2=3 &lt; 5).

2. **Row 8 structure**  
   For row 8, no combination reaches 5 points using only solo activities. The seed design forces use of CoX or ToA. Changing row 8 to allow a 5-point path with solo activities (e.g., different point distribution) would remove this dependency.

### 7.3 Option C: Documentation

Document that:
- Row 8 in the seed requires group activities (CoX or ToA).
- Strategy comparison tests should use teams with comparable schedule overlap.
- For deterministic strategy comparison, use always-online schedules.

---

## 8. Verification Queries

### 8.1 Row 8 Total Points

```csharp
var row8 = event.Rows.FirstOrDefault(r => r.Index == 8);
var totalPoints = row8?.Tiles.Sum(t => t.Points) ?? 0;
// Result: 10 (>= 5 unlock threshold)
```

### 8.2 Player Capabilities for Row 8

```csharp
// Row 8 tiles requiring quest.sote: CoX (t3-r8), ToA (t4-r8)
// Alpha: Carol, Dave have quest.sote
// Beta: Grace, Henry have quest.sote
// Both teams can work on row 8 CoX/ToA tiles
```

### 8.3 Activity Overlap

```csharp
// Row 8 activities: runecraft, mining, cox, toa
// Rows 9-19: Each activity appears on 2-5 locked rows
// Overlap does not prevent completion; it only affects ComboUnlocking's penalty calculation
```

---

## 9. Summary

| Finding | Detail |
|---------|--------|
| **Row 8 structure** | 4 tiles (1–4 pts), 10 pts total. Requires 5 pts to unlock row 9. |
| **5-point paths** | Mining+CoX or Runecraft+ToA. Both need CoX or ToA. |
| **CoX/ToA** | Group-only (2+ players). Both teams have 2 players with quest.sote. |
| **Schedule** | Alpha and Beta have different schedules. Overlap for 2+ players may differ. |
| **Root cause** | Likely insufficient schedule overlap for Team Alpha to run CoX/ToA, causing RowUnlocking to stall at row 8. |
| **Fix** | Use comparable schedules for both teams in strategy comparison, or use always-online schedules for deterministic tests. |

---

## Appendix: Full Row 8 Tile Details (Winter Bingo)

| Tile Key | Name | Points | Activity | RequiredCount | Requirements | Drop Keys |
|----------|------|--------|----------|---------------|-------------|-----------|
| t1-r8 | Runecraft R8 | 1 | skilling.runecraft | 1 | [] | essence.crafted |
| t2-r8 | Mining R8 | 2 | skilling.mining | 1 | [] | ore.mined |
| t3-r8 | Cox R8 | 3 | raid.cox | 1 | [quest.sote] | loot.cox, unique.cox_prayer_scroll |
| t4-r8 | Toa R8 | 4 | raid.toa | 1 | [quest.sote] | loot.toa, unique.toa_ring |

**Modifiers for row 8:** None (GetModifiersForTile returns [] for row 8).
