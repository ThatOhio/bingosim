using BingoSim.Application.Interfaces;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.Interfaces;
using BingoSim.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BingoSim.Application.Services;

/// <summary>
/// Development seed data service for manual testing. Slices 1–3 only (Players, Activities, Events).
/// TODO Slice 4: Extend for Teams/Strategy by adding SeedTeamsAsync, SeedStrategiesAsync, and stable keys for reset.
/// </summary>
public class DevSeedService(
    IPlayerProfileRepository playerRepo,
    IActivityDefinitionRepository activityRepo,
    IEventRepository eventRepo,
    ILogger<DevSeedService> logger) : IDevSeedService
{
    /// <summary>
    /// Stable names for reset. Update when adding new seed players.
    /// </summary>
    private static readonly string[] SeedPlayerNames =
    [
        "Alice", "Bob", "Carol", "Dave", "Eve", "Frank", "Grace", "Henry"
    ];

    /// <summary>
    /// Stable keys for reset. Update when adding new seed activities.
    /// </summary>
    private static readonly string[] SeedActivityKeys =
    [
        "boss.zulrah", "boss.vorkath", "skilling.runecraft", "skilling.mining",
        "raid.cox", "raid.toa"
    ];

    /// <summary>
    /// Stable names for reset. Update when adding new seed events.
    /// </summary>
    private static readonly string[] SeedEventNames =
    [
        "Winter Bingo 2025", "Spring League Bingo"
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Dev seed: starting idempotent seed for Slices 1–3");

        await SeedPlayersAsync(cancellationToken);
        var activityIdsByKey = await SeedActivitiesAsync(cancellationToken);
        await SeedEventsAsync(activityIdsByKey, cancellationToken);

        logger.LogInformation("Dev seed: completed");
    }

    public async Task ResetAndSeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Dev seed: resetting seed-tagged data");

        // Reverse dependency order: Events -> Activities -> Players
        foreach (var name in SeedEventNames)
        {
            var evt = await eventRepo.GetByNameAsync(name, cancellationToken);
            if (evt is not null)
            {
                await eventRepo.DeleteAsync(evt.Id, cancellationToken);
                logger.LogInformation("Dev seed: deleted event '{EventName}'", name);
            }
        }

        foreach (var key in SeedActivityKeys)
        {
            var activity = await activityRepo.GetByKeyAsync(key, cancellationToken);
            if (activity is not null)
            {
                await activityRepo.DeleteAsync(activity.Id, cancellationToken);
                logger.LogInformation("Dev seed: deleted activity '{ActivityKey}'", key);
            }
        }

        foreach (var name in SeedPlayerNames)
        {
            var profile = await playerRepo.GetByNameAsync(name, cancellationToken);
            if (profile is not null)
            {
                await playerRepo.DeleteAsync(profile.Id, cancellationToken);
                logger.LogInformation("Dev seed: deleted player '{PlayerName}'", name);
            }
        }

        logger.LogInformation("Dev seed: reset complete, re-seeding");
        await SeedAsync(cancellationToken);
    }

    private async Task SeedPlayersAsync(CancellationToken cancellationToken)
    {
        var definitions = GetPlayerSeedDefinitions();

        foreach (var def in definitions)
        {
            var existing = await playerRepo.GetByNameAsync(def.Name, cancellationToken);

            if (existing is not null)
            {
                existing.UpdateSkillTimeMultiplier(def.SkillTimeMultiplier);
                existing.SetCapabilities(def.Capabilities);
                existing.SetWeeklySchedule(def.WeeklySchedule);
                await playerRepo.UpdateAsync(existing, cancellationToken);
                logger.LogInformation("Dev seed: updated player '{Name}'", def.Name);
            }
            else
            {
                var profile = new PlayerProfile(def.Name, def.SkillTimeMultiplier);
                profile.SetCapabilities(def.Capabilities);
                profile.SetWeeklySchedule(def.WeeklySchedule);
                await playerRepo.AddAsync(profile, cancellationToken);
                logger.LogInformation("Dev seed: created player '{Name}'", def.Name);
            }
        }
    }

    private static List<PlayerSeedDef> GetPlayerSeedDefinitions()
    {
        return
        [
            new PlayerSeedDef("Alice", 0.85m, [new Capability("quest.ds2", "Dragon Slayer II")], new WeeklySchedule([new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 120), new ScheduledSession(DayOfWeek.Wednesday, new TimeOnly(19, 0), 90), new ScheduledSession(DayOfWeek.Friday, new TimeOnly(20, 0), 180)])),
            new PlayerSeedDef("Bob", 1.0m, [new Capability("item.dragon_hunter_lance", "Dragon Hunter Lance"), new Capability("quest.ds2", "Dragon Slayer II")], new WeeklySchedule([new ScheduledSession(DayOfWeek.Tuesday, new TimeOnly(17, 0), 60), new ScheduledSession(DayOfWeek.Tuesday, new TimeOnly(21, 0), 90), new ScheduledSession(DayOfWeek.Thursday, new TimeOnly(18, 0), 120), new ScheduledSession(DayOfWeek.Saturday, new TimeOnly(10, 0), 240)])),
            new PlayerSeedDef("Carol", 1.15m, [new Capability("quest.sote", "Song of the Elves")], new WeeklySchedule([new ScheduledSession(DayOfWeek.Monday, new TimeOnly(12, 0), 45), new ScheduledSession(DayOfWeek.Monday, new TimeOnly(18, 0), 90), new ScheduledSession(DayOfWeek.Wednesday, new TimeOnly(18, 0), 60), new ScheduledSession(DayOfWeek.Sunday, new TimeOnly(14, 0), 180)])),
            new PlayerSeedDef("Dave", 0.92m, [new Capability("item.dragon_hunter_lance", "Dragon Hunter Lance"), new Capability("quest.sote", "Song of the Elves")], new WeeklySchedule([new ScheduledSession(DayOfWeek.Tuesday, new TimeOnly(19, 0), 120), new ScheduledSession(DayOfWeek.Thursday, new TimeOnly(19, 0), 120), new ScheduledSession(DayOfWeek.Saturday, new TimeOnly(9, 0), 90), new ScheduledSession(DayOfWeek.Saturday, new TimeOnly(15, 0), 90)])),
            new PlayerSeedDef("Eve", 1.05m, [], new WeeklySchedule([new ScheduledSession(DayOfWeek.Monday, new TimeOnly(20, 0), 60), new ScheduledSession(DayOfWeek.Wednesday, new TimeOnly(20, 0), 60), new ScheduledSession(DayOfWeek.Friday, new TimeOnly(20, 0), 90), new ScheduledSession(DayOfWeek.Sunday, new TimeOnly(10, 0), 120)])),
            new PlayerSeedDef("Frank", 0.78m, [new Capability("quest.ds2", "Dragon Slayer II"), new Capability("item.dragon_hunter_lance", "Dragon Hunter Lance")], new WeeklySchedule([new ScheduledSession(DayOfWeek.Tuesday, new TimeOnly(17, 30), 150), new ScheduledSession(DayOfWeek.Thursday, new TimeOnly(17, 30), 150), new ScheduledSession(DayOfWeek.Saturday, new TimeOnly(8, 0), 180), new ScheduledSession(DayOfWeek.Saturday, new TimeOnly(14, 0), 120)])),
            new PlayerSeedDef("Grace", 1.22m, [new Capability("quest.sote", "Song of the Elves")], new WeeklySchedule([new ScheduledSession(DayOfWeek.Monday, new TimeOnly(19, 0), 90), new ScheduledSession(DayOfWeek.Friday, new TimeOnly(19, 0), 120), new ScheduledSession(DayOfWeek.Sunday, new TimeOnly(12, 0), 240)])),
            new PlayerSeedDef("Henry", 0.95m, [new Capability("quest.ds2", "Dragon Slayer II"), new Capability("quest.sote", "Song of the Elves")], new WeeklySchedule([new ScheduledSession(DayOfWeek.Tuesday, new TimeOnly(18, 0), 90), new ScheduledSession(DayOfWeek.Wednesday, new TimeOnly(18, 0), 90), new ScheduledSession(DayOfWeek.Thursday, new TimeOnly(18, 0), 90), new ScheduledSession(DayOfWeek.Saturday, new TimeOnly(10, 0), 180)])),
        ];
    }

    private async Task<Dictionary<string, Guid>> SeedActivitiesAsync(CancellationToken cancellationToken)
    {
        var definitions = GetActivitySeedDefinitions();
        var idsByKey = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (var def in definitions)
        {
            var existing = await activityRepo.GetByKeyAsync(def.Key, cancellationToken);

            if (existing is not null)
            {
                existing.UpdateName(def.Name);
                existing.SetModeSupport(def.ModeSupport);
                existing.SetAttempts(def.Attempts);
                existing.SetGroupScalingBands(def.GroupScalingBands);
                await activityRepo.UpdateAsync(existing, cancellationToken);
                idsByKey[def.Key] = existing.Id;
                logger.LogInformation("Dev seed: updated activity '{Key}'", def.Key);
            }
            else
            {
                var activity = new ActivityDefinition(def.Key, def.Name, def.ModeSupport);
                activity.SetAttempts(def.Attempts);
                activity.SetGroupScalingBands(def.GroupScalingBands);
                await activityRepo.AddAsync(activity, cancellationToken);
                idsByKey[def.Key] = activity.Id;
                logger.LogInformation("Dev seed: created activity '{Key}'", def.Key);
            }
        }

        return idsByKey;
    }

    private static List<ActivitySeedDef> GetActivitySeedDefinitions()
    {
        var dropKeyCommon = "kill.zulrah";
        var dropKeyRare = "unique.tanzanite_fang";
        var dropKeyVorkathCommon = "kill.vorkath";
        var dropKeyVorkathRare = "unique.dragonbone_necklace";
        var dropKeyRc = "essence.crafted";
        var dropKeyMining = "ore.mined";
        var dropKeyCoxCommon = "loot.cox";
        var dropKeyCoxRare = "unique.cox_prayer_scroll";
        var dropKeyToaCommon = "loot.toa";
        var dropKeyToaRare = "unique.toa_ring";

        return
        [
            // Zulrah: 2 loot lines, PerPlayer + PerGroup, Units 1 and 3, bands 1..1, 2..4, 5..8
            new ActivitySeedDef(
                "boss.zulrah", "Zulrah",
                new ActivityModeSupport(true, true, 1, 8),
                [
                    new ActivityAttemptDefinition("standard", RollScope.PerPlayer, new AttemptTimeModel(90, TimeDistribution.Uniform, 30),
                        [new ActivityOutcomeDefinition("common", 3, 4, [new ProgressGrant(dropKeyCommon, 1)]), new ActivityOutcomeDefinition("rare", 1, 4, [new ProgressGrant(dropKeyRare, 3)])]),
                    new ActivityAttemptDefinition("venom", RollScope.PerGroup, new AttemptTimeModel(120, TimeDistribution.Uniform, 20),
                        [new ActivityOutcomeDefinition("common", 2, 3, [new ProgressGrant(dropKeyCommon, 1)]), new ActivityOutcomeDefinition("rare", 1, 3, [new ProgressGrant(dropKeyRare, 3)])]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m), new GroupSizeBand(2, 4, 0.85m, 1.1m), new GroupSizeBand(5, 8, 0.75m, 1.2m)]),
            new ActivitySeedDef(
                "boss.vorkath", "Vorkath",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("main", RollScope.PerPlayer, new AttemptTimeModel(180, TimeDistribution.NormalApprox, 45),
                        [new ActivityOutcomeDefinition("common", 5, 6, [new ProgressGrant(dropKeyVorkathCommon, 1)]), new ActivityOutcomeDefinition("rare", 1, 6, [new ProgressGrant(dropKeyVorkathRare, 3)])]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),
            new ActivitySeedDef(
                "skilling.runecraft", "Runecraft",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("blood", RollScope.PerPlayer, new AttemptTimeModel(60, TimeDistribution.Uniform, 10),
                        [new ActivityOutcomeDefinition("success", 1, 1, [new ProgressGrant(dropKeyRc, 1)])]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),
            new ActivitySeedDef(
                "skilling.mining", "Mining",
                new ActivityModeSupport(true, true, 1, 3),
                [
                    new ActivityAttemptDefinition("ore", RollScope.PerPlayer, new AttemptTimeModel(45, TimeDistribution.Uniform, 15),
                        [new ActivityOutcomeDefinition("one", 2, 3, [new ProgressGrant(dropKeyMining, 1)]), new ActivityOutcomeDefinition("three", 1, 3, [new ProgressGrant(dropKeyMining, 3)])]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m), new GroupSizeBand(2, 3, 0.9m, 1.05m)]),
            new ActivitySeedDef(
                "raid.cox", "Chambers of Xeric",
                new ActivityModeSupport(false, true, 1, 8),
                [
                    new ActivityAttemptDefinition("scavs", RollScope.PerGroup, new AttemptTimeModel(600, TimeDistribution.Uniform, 120),
                        [new ActivityOutcomeDefinition("common", 4, 5, [new ProgressGrant(dropKeyCoxCommon, 1)]), new ActivityOutcomeDefinition("rare", 1, 5, [new ProgressGrant(dropKeyCoxRare, 3)])]),
                    new ActivityAttemptDefinition("olm", RollScope.PerPlayer, new AttemptTimeModel(900, TimeDistribution.Uniform, 180),
                        [new ActivityOutcomeDefinition("common", 3, 4, [new ProgressGrant(dropKeyCoxCommon, 1)]), new ActivityOutcomeDefinition("rare", 1, 4, [new ProgressGrant(dropKeyCoxRare, 3)])]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m), new GroupSizeBand(2, 4, 0.9m, 1.1m), new GroupSizeBand(5, 8, 0.8m, 1.15m)]),
            new ActivitySeedDef(
                "raid.toa", "Tombs of Amascut",
                new ActivityModeSupport(false, true, 1, 8),
                [
                    new ActivityAttemptDefinition("invocation", RollScope.PerPlayer, new AttemptTimeModel(1800, TimeDistribution.Uniform, 300),
                        [new ActivityOutcomeDefinition("common", 3, 4, [new ProgressGrant(dropKeyToaCommon, 1)]), new ActivityOutcomeDefinition("rare", 1, 4, [new ProgressGrant(dropKeyToaRare, 3)])]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m), new GroupSizeBand(2, 4, 0.85m, 1.12m), new GroupSizeBand(5, 8, 0.75m, 1.2m)]),
        ];
    }

    private async Task SeedEventsAsync(Dictionary<string, Guid> activityIdsByKey, CancellationToken cancellationToken)
    {
        var definitions = GetEventSeedDefinitions(activityIdsByKey);

        foreach (var def in definitions)
        {
            var existing = await eventRepo.GetByNameAsync(def.Name, cancellationToken);

            if (existing is not null)
            {
                existing.UpdateDuration(def.Duration);
                existing.SetUnlockPointsRequiredPerRow(5);
                existing.SetRows(def.Rows);
                await eventRepo.UpdateAsync(existing, cancellationToken);
                logger.LogInformation("Dev seed: updated event '{Name}'", def.Name);
            }
            else
            {
                var evt = new Event(def.Name, def.Duration, 5);
                evt.SetRows(def.Rows);
                await eventRepo.AddAsync(evt, cancellationToken);
                logger.LogInformation("Dev seed: created event '{Name}'", def.Name);
            }
        }
    }

    private static List<EventSeedDef> GetEventSeedDefinitions(Dictionary<string, Guid> activityIdsByKey)
    {
        var questDs2 = new Capability("quest.ds2", "Dragon Slayer II");
        var itemDhl = new Capability("item.dragon_hunter_lance", "Dragon Hunter Lance");
        var questSote = new Capability("quest.sote", "Song of the Elves");

        var zulrahId = activityIdsByKey["boss.zulrah"];
        var vorkathId = activityIdsByKey["boss.vorkath"];
        var rcId = activityIdsByKey["skilling.runecraft"];
        var miningId = activityIdsByKey["skilling.mining"];
        var coxId = activityIdsByKey["raid.cox"];
        var toaId = activityIdsByKey["raid.toa"];

        var rowsWinter = new List<Row>
        {
            new(0, [
                new Tile("t1-r0", "Zulrah Kill", 1, 1, [new TileActivityRule(zulrahId, "boss.zulrah", ["kill.zulrah", "unique.tanzanite_fang"], [], [new ActivityModifierRule(itemDhl, 0.9m, null), new ActivityModifierRule(questDs2, null, 1.1m)])]),
                new Tile("t2-r0", "Vorkath Kill", 2, 1, [new TileActivityRule(vorkathId, "boss.vorkath", ["kill.vorkath"], [questDs2], [new ActivityModifierRule(itemDhl, 0.85m, 1.05m)])]),
                new Tile("t3-r0", "Runecraft", 3, 1, [new TileActivityRule(rcId, "skilling.runecraft", ["essence.crafted"], [], [new ActivityModifierRule(questSote, 0.9m, null)])]),
                new Tile("t4-r0", "Cox Loot", 4, 1, [new TileActivityRule(coxId, "raid.cox", ["loot.cox", "unique.cox_prayer_scroll"], [questSote], [new ActivityModifierRule(questSote, 0.95m, 1.1m)])]),
            ]),
            new(1, [
                new Tile("t1-r1", "Mining Ore", 1, 1, [new TileActivityRule(miningId, "skilling.mining", ["ore.mined"], [], [new ActivityModifierRule(questSote, null, 1.15m)])]),
                new Tile("t2-r1", "Toa Loot", 2, 1, [new TileActivityRule(toaId, "raid.toa", ["loot.toa", "unique.toa_ring"], [questSote], [new ActivityModifierRule(questSote, 0.92m, 1.08m)])]),
                new Tile("t3-r1", "Zulrah Unique", 3, 1, [new TileActivityRule(zulrahId, "boss.zulrah", ["unique.tanzanite_fang"], [questDs2], [new ActivityModifierRule(itemDhl, 0.88m, 1.05m)])]),
                new Tile("t4-r1", "Vorkath Unique", 4, 1, [new TileActivityRule(vorkathId, "boss.vorkath", ["unique.dragonbone_necklace"], [questDs2], [new ActivityModifierRule(itemDhl, 0.9m, null)])]),
            ]),
            new(2, [
                new Tile("t1-r2", "Raid Cox", 1, 1, [new TileActivityRule(coxId, "raid.cox", ["loot.cox"], [], [new ActivityModifierRule(questSote, 0.9m, 1.1m)])]),
                new Tile("t2-r2", "Raid Toa", 2, 1, [new TileActivityRule(toaId, "raid.toa", ["loot.toa"], [], [new ActivityModifierRule(questSote, 0.95m, null)])]),
                new Tile("t3-r2", "Skilling Combo", 3, 1, [new TileActivityRule(rcId, "skilling.runecraft", ["essence.crafted"], [], []), new TileActivityRule(miningId, "skilling.mining", ["ore.mined"], [], [])]),
                new Tile("t4-r2", "Boss Combo", 4, 1, [new TileActivityRule(zulrahId, "boss.zulrah", ["kill.zulrah"], [], []), new TileActivityRule(vorkathId, "boss.vorkath", ["kill.vorkath"], [questDs2], [])]),
            ]),
        };

        var rowsSpring = new List<Row>
        {
            new(0, [
                new Tile("s1-r0", "Zulrah Standard", 1, 1, [new TileActivityRule(zulrahId, "boss.zulrah", ["kill.zulrah"], [], [new ActivityModifierRule(questDs2, 0.9m, 1.1m)])]),
                new Tile("s2-r0", "Mining", 2, 1, [new TileActivityRule(miningId, "skilling.mining", ["ore.mined"], [], [new ActivityModifierRule(questSote, null, 1.2m)])]),
                new Tile("s3-r0", "Cox", 3, 1, [new TileActivityRule(coxId, "raid.cox", ["loot.cox", "unique.cox_prayer_scroll"], [questSote], [new ActivityModifierRule(questSote, 0.88m, 1.12m)])]),
                new Tile("s4-r0", "Toa", 4, 1, [new TileActivityRule(toaId, "raid.toa", ["loot.toa"], [questSote], [new ActivityModifierRule(questSote, 0.9m, null)])]),
            ]),
            new(1, [
                new Tile("s1-r1", "Runecraft", 1, 1, [new TileActivityRule(rcId, "skilling.runecraft", ["essence.crafted"], [], [new ActivityModifierRule(questSote, 0.85m, null)])]),
                new Tile("s2-r1", "Vorkath", 2, 1, [new TileActivityRule(vorkathId, "boss.vorkath", ["kill.vorkath", "unique.dragonbone_necklace"], [questDs2], [new ActivityModifierRule(itemDhl, 0.9m, 1.05m)])]),
                new Tile("s3-r1", "Zulrah Rare", 3, 1, [new TileActivityRule(zulrahId, "boss.zulrah", ["unique.tanzanite_fang"], [], [new ActivityModifierRule(itemDhl, 0.92m, 1.08m)])]),
                new Tile("s4-r1", "Multi Activity", 4, 1, [new TileActivityRule(miningId, "skilling.mining", ["ore.mined"], [], []), new TileActivityRule(rcId, "skilling.runecraft", ["essence.crafted"], [], [])]),
            ]),
        };

        return
        [
            new EventSeedDef("Winter Bingo 2025", TimeSpan.FromHours(24), rowsWinter),
            new EventSeedDef("Spring League Bingo", TimeSpan.FromHours(48), rowsSpring),
        ];
    }

    private sealed record PlayerSeedDef(string Name, decimal SkillTimeMultiplier, List<Capability> Capabilities, WeeklySchedule WeeklySchedule);

    private sealed record ActivitySeedDef(string Key, string Name, ActivityModeSupport ModeSupport, List<ActivityAttemptDefinition> Attempts, List<GroupSizeBand> GroupScalingBands);

    private sealed record EventSeedDef(string Name, TimeSpan Duration, List<Row> Rows);
}
