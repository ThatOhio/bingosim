# Real Event Data Template

> **Instructions**: Fill out each section below with your real-world event data. Use the examples as a guide. When complete, share this document and ask the AI to convert it into seed script code.

---

## 1. Event Overview

| Field | Value | Notes |
|-------|-------|-------|
| **Event Name** | | e.g., "Winter Bingo 2025" |
| **Duration (hours)** | | Total event length in hours (e.g., 24, 48, 72) |
| **Unlock Points Per Row** | 5 | Points required to unlock each new row (default: 5) |

---

## 2. Players

List each player who participated (or will participate) in the event. Include their approximate skill level and availability.

| # | Name | Skill Multiplier | Capabilities | Weekly Schedule |
|---|------|------------------|--------------|-----------------|
| 1 | | 1.0 | | |
| 2 | | 1.0 | | |
| 3 | | 1.0 | | |
| 4 | | 1.0 | | |
| 5 | | 1.0 | | |
| 6 | | 1.0 | | |
| 7 | | 1.0 | | |
| 8 | | 1.0 | | |

### Field Descriptions

- **Skill Multiplier**: `1.0` = average, `0.8` = 20% faster, `1.2` = 20% slower
- **Capabilities**: Comma-separated list of capability keys (see Section 4). Examples: `quest.ds2`, `quest.sote`, `item.dragon_hunter_lance`
- **Weekly Schedule**: One entry per session. Format: `Day HH:mm DurationMinutes`. Examples:
  - `Monday 18:00 120` = Monday 6pm, 2 hours
  - `Tuesday 17:00 60, Tuesday 21:00 90` = Two sessions on Tuesday
  - `Saturday 10:00 240` = Saturday 10am, 4 hours

### Example (copy and adapt)

| # | Name | Skill Multiplier | Capabilities | Weekly Schedule |
|---|------|------------------|--------------|-----------------|
| 1 | Alice | 0.85 | quest.ds2 | Monday 18:00 120, Wednesday 19:00 90, Friday 20:00 180 |
| 2 | Bob | 1.0 | item.dragon_hunter_lance, quest.ds2 | Tuesday 17:00 60, Tuesday 21:00 90, Thursday 18:00 120, Saturday 10:00 240 |
| 3 | Carol | 1.15 | quest.sote | Monday 12:00 45, Monday 18:00 90, Wednesday 18:00 60, Sunday 14:00 180 |
| 4 | Dave | 0.92 | item.dragon_hunter_lance, quest.sote | Tuesday 19:00 120, Thursday 19:00 120, Saturday 9:00 90, Saturday 15:00 90 |

---

## 3. Capabilities (Quests, Items, etc.)

Define all capabilities referenced by players or tiles. Use a stable key and a display name.

| Key | Display Name |
|-----|--------------|
| quest.ds2 | Dragon Slayer II |
| quest.sote | Song of the Elves |
| item.dragon_hunter_lance | Dragon Hunter Lance |
| | |
| | |

### Example keys

- `quest.ds2` — Dragon Slayer II (Vorkath, Zulrah)
- `quest.sote` — Song of the Elves (CoX, ToA, Prif skilling)
- `item.dragon_hunter_lance` — DHL (Vorkath/Zulrah time modifier)

---

## 4. Activities

Define each in-game activity (boss, raid, skilling method) that appears on the board.

### Activity 1

| Field | Value |
|-------|-------|
| **Key** | e.g., boss.zulrah |
| **Name** | e.g., Zulrah |
| **Supports Solo** | true / false |
| **Supports Group** | true / false |
| **Min Group Size** | (if group) e.g., 1 |
| **Max Group Size** | (if group) e.g., 8 |

**Attempts** (loot lines — one per row):

| Attempt Key | Roll Scope | Baseline Time (sec) | Time Distribution | Variance (sec) | Outcomes |
|-------------|------------|---------------------|-------------------|----------------|----------|
| standard | PerPlayer | 90 | Uniform | 30 | common 3/4 → kill.zulrah 1; rare 1/4 → unique.tanzanite_fang 3 |
| venom | PerGroup | 120 | Uniform | 20 | common 2/3 → kill.zulrah 1; rare 1/3 → unique.tanzanite_fang 3 |

**Group Scaling Bands** (optional — for group activities):

| Min Size | Max Size | Time Multiplier | Probability Multiplier |
|----------|----------|-----------------|------------------------|
| 1 | 1 | 1.0 | 1.0 |
| 2 | 4 | 0.85 | 1.1 |
| 5 | 8 | 0.75 | 1.2 |

### Activity 2

| Field | Value |
|-------|-------|
| **Key** | |
| **Name** | |
| **Supports Solo** | |
| **Supports Group** | |
| **Min Group Size** | |
| **Max Group Size** | |

**Attempts**:

| Attempt Key | Roll Scope | Baseline Time (sec) | Time Distribution | Variance (sec) | Outcomes |
|-------------|------------|---------------------|-------------------|----------------|----------|
| | PerPlayer / PerGroup | | Uniform / NormalApprox | | Format: outcomeName weightNum/weightDenom → dropKey units |

**Group Scaling Bands** (if applicable):

| Min Size | Max Size | Time Multiplier | Probability Multiplier |
|----------|----------|-----------------|------------------------|
| | | | |

### Activity 3, 4, … (add as many as needed)

*(Repeat the structure above for each activity.)*

### Outcome Format

`outcomeName weightNum/weightDenom → dropKey units`

For variable amounts (e.g. boss drops 50–100 arrows per kill), use a range:

`outcomeName weightNum/weightDenom → dropKey minUnits-maxUnits`

Examples:
- `common 3/4 → kill.zulrah 1` = 75% chance, grants 1 unit of `kill.zulrah`
- `rare 1/4 → unique.tanzanite_fang 3` = 25% chance, grants 3 units of `unique.tanzanite_fang`
- `arrows 1/1 → item.arrows 50-100` = 100% chance, grants 50–100 units (sampled uniformly per attempt)

---

## 5. Event Rows and Tiles

Each row has exactly 4 tiles with points 1, 2, 3, 4. Fill out the board row by row.

### Row 0

| Tile | Key | Name | Points | Required Count | Activity Key(s) | Drop Keys | Requirements | Modifiers |
|------|-----|------|--------|----------------|-----------------|-----------|--------------|-----------|
| 1 | t1-r0 | | 1 | 1 | | | | |
| 2 | t2-r0 | | 2 | 1 | | | | |
| 3 | t3-r0 | | 3 | 1 | | | | |
| 4 | t4-r0 | | 4 | 1 | | | | |

### Row 1

| Tile | Key | Name | Points | Required Count | Activity Key(s) | Drop Keys | Requirements | Modifiers |
|------|-----|------|--------|----------------|-----------------|-----------|--------------|-----------|
| 1 | t1-r1 | | 1 | 1 | | | | |
| 2 | t2-r1 | | 2 | 1 | | | | |
| 3 | t3-r1 | | 3 | 1 | | | | |
| 4 | t4-r1 | | 4 | 1 | | | | |

### Rows 2–N

*(Continue for each row. Add more row sections as needed.)*

### Field Descriptions

- **Key**: Unique within event. Convention: `t{points}-r{rowIndex}` (e.g., `t1-r0`, `t3-r5`)
- **Name**: Display name (e.g., "Zulrah R0", "Combo Zulrah+Vorkath x2")
- **Points**: 1, 2, 3, or 4 (one of each per row)
- **Required Count**: How many completions needed (usually 1, sometimes 2 or 3)
- **Activity Key(s)**: Single activity key, or comma-separated for combo tiles (e.g., `boss.zulrah` or `boss.zulrah,boss.vorkath`)
- **Drop Keys**: Comma-separated drop keys this tile accepts (e.g., `kill.zulrah,unique.tanzanite_fang`). Use common drops for most tiles; rare-only for harder tiles.
- **Requirements**: Capability keys required to attempt (e.g., `quest.ds2`). Comma-separated. Leave empty if none.
- **Modifiers**: Optional. Format: `capabilityKey:timeMult,probMult`. Example: `item.dragon_hunter_lance:0.9,1.1` = DHL gives 0.9× time, 1.1× probability. Comma-separated for multiple.

### Example Tile Rows

**Row 0 (simple)**:
| Tile | Key | Name | Points | Req | Activity | Drops | Reqs | Mods |
|------|-----|------|--------|-----|----------|-------|------|------|
| 1 | t1-r0 | Zulrah R0 | 1 | 1 | boss.zulrah | kill.zulrah,unique.tanzanite_fang | | |
| 2 | t2-r0 | Vorkath R0 | 2 | 1 | boss.vorkath | kill.vorkath,unique.dragonbone_necklace | quest.ds2 | |
| 3 | t3-r0 | Runecraft R0 | 3 | 1 | skilling.runecraft | essence.crafted | | |
| 4 | t4-r0 | Mining R0 | 4 | 1 | skilling.mining | ore.mined | | |

**Row 1 (with modifier)**:
| Tile | Key | Name | Points | Req | Activity | Drops | Reqs | Mods |
|------|-----|------|--------|-----|----------|-------|------|------|
| 1 | t1-r1 | Zulrah R1 | 1 | 1 | boss.zulrah | kill.zulrah,unique.tanzanite_fang | | item.dragon_hunter_lance:0.9,1.1 |
| … | … | … | … | … | … | … | … | … |

**Combo tile example**:
| Tile | Key | Name | Points | Req | Activity | Drops | Reqs | Mods |
|------|-----|------|--------|-----|----------|-------|------|------|
| 3 | t3-r2 | Combo Zulrah+Vorkath | 3 | 1 | boss.zulrah,boss.vorkath | kill.zulrah,kill.vorkath | quest.ds2 | |

---

## 6. Teams

List teams for this event. Each team has a name, strategy, and player list.

### Team 1

| Field | Value |
|-------|-------|
| **Team Name** | e.g., Team Alpha |
| **Strategy** | RowUnlocking / GreedyPoints / ComboUnlocking / RowRush |
| **Player Names** | Comma-separated (e.g., Alice, Bob, Carol, Dave) |

### Team 2

| Field | Value |
|-------|-------|
| **Team Name** | |
| **Strategy** | |
| **Player Names** | |

### Team 3, 4, … (add as needed)

---

## 7. Drop Key Reference (by Activity)

Use this section to document which drop keys each activity uses. The AI will use this when generating tile rules.

| Activity Key | Common Drop Keys | Rare Drop Keys |
|--------------|------------------|----------------|
| boss.zulrah | kill.zulrah, unique.tanzanite_fang | unique.tanzanite_fang |
| boss.vorkath | kill.vorkath, unique.dragonbone_necklace | unique.dragonbone_necklace |
| skilling.runecraft | essence.crafted | essence.crafted |
| skilling.mining | ore.mined | ore.mined |
| raid.cox | loot.cox, unique.cox_prayer_scroll | unique.cox_prayer_scroll |
| raid.toa | loot.toa, unique.toa_ring | unique.toa_ring |
| | | |

---

## 8. Conversion Instructions (for AI)

When this document is complete, use it to:

1. Add or extend `DevSeedService` (or a dedicated `RealEventSeedService`) with:
   - All players from Section 2
   - All capabilities from Section 3
   - All activities from Section 4 (with attempts, outcomes, group bands)
   - The event from Section 1 with rows/tiles from Section 5
   - All teams from Section 6

2. Ensure activity keys, tile keys, and capability keys are consistent throughout.

3. For combo tiles: create `TileActivityRule` entries for each activity in the combo, with appropriate drop keys per activity.

4. Use the same patterns as existing `DevSeedService` (e.g., `PlayerSeedDef`, `ActivitySeedDef`, `EventSeedDef`, `BuildEventRows`-style logic or explicit row/tile construction).
