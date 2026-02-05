# Real Event Data Template

This folder contains templates for capturing real-world OSRS Bingo event data. Fill out the template with your event details, then ask the AI to convert it into seed script code.

## Workflow

1. **Copy** `EVENT_TEMPLATE.md` to a new file (e.g., `Winter_Bingo_2025.md`).
2. **Fill out** all sections with your real event data.
3. **Share** the filled-out document with the AI and request: *"Convert this RealEventData document into seed script code for DevSeedService."*
4. The AI will generate C# code to add to `DevSeedService.cs` (or a dedicated real-event seeder).

## File Structure

| File | Purpose |
|------|---------|
| `EVENT_TEMPLATE.md` | Master template with all sections and field descriptions |
| `EXAMPLE_FILLED.md` | Minimal filled example (Winter Bingo 2025) for reference |
| `README.md` | This file — usage instructions |

## Data Model Quick Reference

- **Event**: Name, duration (hours), unlock points per row
- **Row**: Index (0-based), exactly 4 tiles with points 1, 2, 3, 4
- **Tile**: Key, display name, points (1–4), required count, activity rules
- **Activity**: Key, name, solo/group support, attempts (loot lines), group scaling
- **Player**: Name, skill multiplier, capabilities (quests/items), weekly schedule
- **Team**: Name, event, player list, strategy

## Notes

- **Activity keys** must be unique and use a convention like `boss.zulrah`, `skilling.runecraft`, `raid.cox`.
- **Tile keys** must be unique within the event (e.g., `t1-r0`, `t2-r0`, …).
- **Capabilities** gate eligibility (e.g., `quest.ds2` for Vorkath) or provide modifiers.
- **Drop keys** are internal identifiers for progress (e.g., `kill.zulrah`, `unique.tanzanite_fang`).
