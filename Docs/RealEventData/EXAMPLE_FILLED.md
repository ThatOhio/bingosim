# Example: Winter Bingo 2025 (Minimal Filled Example)

> This is a minimal example showing how to fill out the template. Use it as a reference when filling your own event.

---

## 1. Event Overview

| Field | Value | Notes |
|-------|-------|-------|
| **Event Name** | Winter Bingo 2025 | |
| **Duration (hours)** | 24 | |
| **Unlock Points Per Row** | 5 | |

---

## 2. Players

| # | Name | Skill Multiplier | Capabilities | Weekly Schedule |
|---|------|------------------|--------------|-----------------|
| 1 | Alice | 0.85 | quest.ds2 | Monday 18:00 120, Wednesday 19:00 90, Friday 20:00 180 |
| 2 | Bob | 1.0 | item.dragon_hunter_lance, quest.ds2 | Tuesday 17:00 60, Tuesday 21:00 90, Thursday 18:00 120, Saturday 10:00 240 |
| 3 | Carol | 1.15 | quest.sote | Monday 12:00 45, Monday 18:00 90, Wednesday 18:00 60, Sunday 14:00 180 |
| 4 | Dave | 0.92 | item.dragon_hunter_lance, quest.sote | Tuesday 19:00 120, Thursday 19:00 120, Saturday 9:00 90, Saturday 15:00 90 |
| 5 | Eve | 1.05 | | Monday 20:00 60, Wednesday 20:00 60, Friday 20:00 90, Sunday 10:00 120 |
| 6 | Frank | 0.78 | quest.ds2, item.dragon_hunter_lance | Tuesday 17:30 150, Thursday 17:30 150, Saturday 8:00 180, Saturday 14:00 120 |
| 7 | Grace | 1.22 | quest.sote | Monday 19:00 90, Friday 19:00 120, Sunday 12:00 240 |
| 8 | Henry | 0.95 | quest.ds2, quest.sote | Tuesday 18:00 90, Wednesday 18:00 90, Thursday 18:00 90, Saturday 10:00 180 |

---

## 3. Capabilities

| Key | Display Name |
|-----|--------------|
| quest.ds2 | Dragon Slayer II |
| quest.sote | Song of the Elves |
| item.dragon_hunter_lance | Dragon Hunter Lance |

---

## 4. Activities (Abbreviated — 2 examples)

### Activity 1: Zulrah

| Field | Value |
|-------|-------|
| **Key** | boss.zulrah |
| **Name** | Zulrah |
| **Supports Solo** | true |
| **Supports Group** | true |
| **Min Group Size** | 1 |
| **Max Group Size** | 8 |

**Attempts**:
| Attempt Key | Roll Scope | Baseline Time | Distribution | Variance | Outcomes |
|-------------|------------|---------------|--------------|----------|----------|
| standard | PerPlayer | 90 | Uniform | 30 | common 3/4 → kill.zulrah 1; rare 1/4 → unique.tanzanite_fang 3 |
| venom | PerGroup | 120 | Uniform | 20 | common 2/3 → kill.zulrah 1; rare 1/3 → unique.tanzanite_fang 3 |

**Group Scaling Bands**:
| Min | Max | Time Mult | Prob Mult |
|-----|-----|-----------|------------|
| 1 | 1 | 1.0 | 1.0 |
| 2 | 4 | 0.85 | 1.1 |
| 5 | 8 | 0.75 | 1.2 |

### Activity 2: Vorkath

| Field | Value |
|-------|-------|
| **Key** | boss.vorkath |
| **Name** | Vorkath |
| **Supports Solo** | true |
| **Supports Group** | false |

**Attempts**:
| Attempt Key | Roll Scope | Baseline Time | Distribution | Variance | Outcomes |
|-------------|------------|---------------|--------------|----------|----------|
| main | PerPlayer | 180 | NormalApprox | 45 | common 5/6 → kill.vorkath 1; rare 1/6 → unique.dragonbone_necklace 3 |

*(Add boss.vorkath, skilling.runecraft, skilling.mining, raid.cox, raid.toa similarly.)*

---

## 5. Event Rows (First 3 rows as example)

### Row 0

| Tile | Key | Name | Points | Req | Activity | Drops | Reqs | Mods |
|------|-----|------|--------|-----|----------|-------|------|------|
| 1 | t1-r0 | Zulrah R0 | 1 | 1 | boss.zulrah | kill.zulrah,unique.tanzanite_fang | | |
| 2 | t2-r0 | Vorkath R0 | 2 | 1 | boss.vorkath | kill.vorkath,unique.dragonbone_necklace | quest.ds2 | |
| 3 | t3-r0 | Runecraft R0 | 3 | 1 | skilling.runecraft | essence.crafted | | |
| 4 | t4-r0 | Mining R0 | 4 | 1 | skilling.mining | ore.mined | | |

### Row 1

| Tile | Key | Name | Points | Req | Activity | Drops | Reqs | Mods |
|------|-----|------|--------|-----|----------|-------|------|------|
| 1 | t1-r1 | Zulrah R1 | 1 | 1 | boss.zulrah | kill.zulrah,unique.tanzanite_fang | | item.dragon_hunter_lance:0.9,1.1 |
| 2 | t2-r1 | Vorkath R1 | 2 | 1 | boss.vorkath | kill.vorkath,unique.dragonbone_necklace | quest.ds2 | |
| 3 | t3-r1 | Runecraft R1 | 3 | 1 | skilling.runecraft | essence.crafted | | |
| 4 | t4-r1 | Mining R1 | 4 | 1 | skilling.mining | ore.mined | | |

### Row 2 (with combo tile)

| Tile | Key | Name | Points | Req | Activity | Drops | Reqs | Mods |
|------|-----|------|--------|-----|----------|-------|------|------|
| 1 | t1-r2 | Zulrah R2 | 1 | 1 | boss.zulrah | kill.zulrah,unique.tanzanite_fang | | |
| 2 | t2-r2 | Vorkath R2 | 2 | 1 | boss.vorkath | kill.vorkath,unique.dragonbone_necklace | quest.ds2 | |
| 3 | t3-r2 | Combo Zulrah+Vorkath | 3 | 1 | boss.zulrah,boss.vorkath | kill.zulrah,kill.vorkath | quest.ds2 | |
| 4 | t4-r2 | Mining R2 | 4 | 1 | skilling.mining | ore.mined | | |

*(Continue for all 20 rows in a real event.)*

---

## 6. Teams

### Team 1

| Field | Value |
|-------|-------|
| **Team Name** | Team Alpha |
| **Strategy** | RowUnlocking |
| **Player Names** | Alice, Bob, Carol, Dave |

### Team 2

| Field | Value |
|-------|-------|
| **Team Name** | Team Beta |
| **Strategy** | RowUnlocking |
| **Player Names** | Eve, Frank, Grace, Henry |

---

## 7. Drop Key Reference

| Activity Key | Common Drop Keys | Rare Drop Keys |
|--------------|------------------|----------------|
| boss.zulrah | kill.zulrah, unique.tanzanite_fang | unique.tanzanite_fang |
| boss.vorkath | kill.vorkath, unique.dragonbone_necklace | unique.dragonbone_necklace |
| skilling.runecraft | essence.crafted | essence.crafted |
| skilling.mining | ore.mined | ore.mined |
| raid.cox | loot.cox, unique.cox_prayer_scroll | unique.cox_prayer_scroll |
| raid.toa | loot.toa, unique.toa_ring | unique.toa_ring |
