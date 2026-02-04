using BingoSim.Application.Interfaces;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.Interfaces;
using BingoSim.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BingoSim.Application.Services;

/// <summary>
/// Development seed data service for manual testing. Slices 1–4 (Players, Activities, Events, Teams + Strategy).
/// </summary>
public class DevSeedService(
    IPlayerProfileRepository playerRepo,
    IActivityDefinitionRepository activityRepo,
    IEventRepository eventRepo,
    ITeamRepository teamRepo,
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

    /// <summary>
    /// Stable team names for seed events (event name + team name).
    /// </summary>
    private static readonly string[] SeedTeamNames =
    [
        "Team Alpha", "Team Beta"
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Dev seed: starting idempotent seed for Slices 1–4");

        await SeedPlayersAsync(cancellationToken);
        var activityIdsByKey = await SeedActivitiesAsync(cancellationToken);
        await SeedEventsAsync(activityIdsByKey, cancellationToken);
        await SeedTeamsAsync(cancellationToken);

        logger.LogInformation("Dev seed: completed");
    }

    public async Task ResetAndSeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Dev seed: resetting seed-tagged data");

        // Reverse dependency order: Teams (StrategyConfigs + TeamPlayers) -> Events -> Activities -> Players
        foreach (var name in SeedEventNames)
        {
            var evt = await eventRepo.GetByNameAsync(name, cancellationToken);
            if (evt is not null)
            {
                await teamRepo.DeleteAllByEventIdAsync(evt.Id, cancellationToken);
                logger.LogInformation("Dev seed: deleted teams for event '{EventName}'", name);
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

    private const int SeedEventRowCount = 20;

    private static List<EventSeedDef> GetEventSeedDefinitions(Dictionary<string, Guid> activityIdsByKey)
    {
        var questDs2 = new Capability("quest.ds2", "Dragon Slayer II");
        var itemDhl = new Capability("item.dragon_hunter_lance", "Dragon Hunter Lance");
        var questSote = new Capability("quest.sote", "Song of the Elves");

        var activityIds = new[]
        {
            activityIdsByKey["boss.zulrah"],
            activityIdsByKey["boss.vorkath"],
            activityIdsByKey["skilling.runecraft"],
            activityIdsByKey["skilling.mining"],
            activityIdsByKey["raid.cox"],
            activityIdsByKey["raid.toa"],
        };

        var activityKeys = new[] { "boss.zulrah", "boss.vorkath", "skilling.runecraft", "skilling.mining", "raid.cox", "raid.toa" };

        var rowsWinter = BuildEventRows("t", activityIds, activityKeys, questDs2, itemDhl, questSote);
        var rowsSpring = BuildEventRows("s", activityIds, activityKeys, questDs2, itemDhl, questSote);

        return
        [
            new EventSeedDef("Winter Bingo 2025", TimeSpan.FromHours(24), rowsWinter),
            new EventSeedDef("Spring League Bingo", TimeSpan.FromHours(48), rowsSpring),
        ];
    }

    /// <summary>
    /// Builds 20 rows of tiles with activity reuse: same activities appear close together (consecutive rows)
    /// and distributed (e.g., row 0 and row 16). Includes variety: RequiredCount 1-3, modifiers,
    /// requirements, and combo tiles.
    /// </summary>
    private static List<Row> BuildEventRows(
        string keyPrefix,
        Guid[] activityIds,
        string[] activityKeys,
        Capability questDs2,
        Capability itemDhl,
        Capability questSote)
    {
        var dropKeysByActivity = new Dictionary<string, (string[] common, string[] rare)>(StringComparer.Ordinal)
        {
            ["boss.zulrah"] = (["kill.zulrah", "unique.tanzanite_fang"], ["unique.tanzanite_fang"]),
            ["boss.vorkath"] = (["kill.vorkath", "unique.dragonbone_necklace"], ["unique.dragonbone_necklace"]),
            ["skilling.runecraft"] = (["essence.crafted"], ["essence.crafted"]),
            ["skilling.mining"] = (["ore.mined"], ["ore.mined"]),
            ["raid.cox"] = (["loot.cox", "unique.cox_prayer_scroll"], ["loot.cox"]),
            ["raid.toa"] = (["loot.toa", "unique.toa_ring"], ["loot.toa"]),
        };

        var rows = new List<Row>(SeedEventRowCount);

        for (var rowIdx = 0; rowIdx < SeedEventRowCount; rowIdx++)
        {
            var tiles = new List<Tile>(4);

            for (var tilePos = 0; tilePos < 4; tilePos++)
            {
                var points = tilePos + 1;
                var key = $"{keyPrefix}{points}-r{rowIdx}";

                var activityIdx = (rowIdx + tilePos) % 6;
                var actId = activityIds[activityIdx];
                var actKey = activityKeys[activityIdx];
                var (commonDrops, rareDrops) = dropKeysByActivity[actKey];

                var requiredCount = GetRequiredCountForTile(rowIdx, tilePos);
                var useCombo = rowIdx % 7 == 2 && tilePos == 2;
                var useRareOnly = rowIdx % 5 == 1 && tilePos == 3;

                List<TileActivityRule> rules;
                string name;

                if (useCombo)
                {
                    var nextIdx = (activityIdx + 1) % 6;
                    var nextId = activityIds[nextIdx];
                    var nextKey = activityKeys[nextIdx];
                    var (nextCommon, _) = dropKeysByActivity[nextKey];
                    rules =
                    [
                        new TileActivityRule(actId, actKey, commonDrops, actKey == "boss.vorkath" ? [questDs2] : [], []),
                        new TileActivityRule(nextId, nextKey, nextCommon, nextKey == "boss.vorkath" ? [questDs2] : [], []),
                    ];
                    name = GetComboTileName(actKey, nextKey, requiredCount);
                }
                else
                {
                    var dropKeys = useRareOnly ? rareDrops : commonDrops;
                    var reqs = GetRequirementsForTile(actKey, rowIdx, tilePos, questDs2, questSote);
                    var mods = GetModifiersForTile(actKey, rowIdx, tilePos, questDs2, itemDhl, questSote);
                    rules = [new TileActivityRule(actId, actKey, dropKeys, reqs, mods)];
                    name = GetTileName(actKey, rowIdx, tilePos, requiredCount);
                }

                tiles.Add(new Tile(key, name, points, requiredCount, rules));
            }

            rows.Add(new Row(rowIdx, tiles));
        }

        return rows;
    }

    private static int GetRequiredCountForTile(int rowIdx, int tilePos)
    {
        if (rowIdx == 5 && tilePos == 1) return 2;
        if (rowIdx == 12 && tilePos == 3) return 2;
        if (rowIdx == 18 && tilePos == 0) return 3;
        return 1;
    }

    private static string GetShortActivityName(string actKey) => actKey switch
    {
        "boss.zulrah" => "Zulrah",
        "boss.vorkath" => "Vorkath",
        "skilling.runecraft" => "Runecraft",
        "skilling.mining" => "Mining",
        "raid.cox" => "Cox",
        "raid.toa" => "Toa",
        _ => "Task",
    };

    private static string GetTileName(string actKey, int rowIdx, int tilePos, int requiredCount)
    {
        var shortName = GetShortActivityName(actKey);
        var suffix = requiredCount > 1 ? $" x{requiredCount}" : "";
        return $"{shortName} R{rowIdx}{suffix}".Trim();
    }

    private static string GetComboTileName(string actKey1, string actKey2, int requiredCount)
    {
        var n1 = GetShortActivityName(actKey1);
        var n2 = GetShortActivityName(actKey2);
        var suffix = requiredCount > 1 ? $" x{requiredCount}" : "";
        return $"Combo {n1}+{n2}{suffix}".Trim();
    }

    private static List<Capability> GetRequirementsForTile(string actKey, int rowIdx, int tilePos, Capability questDs2, Capability questSote)
    {
        if (actKey is "boss.vorkath" or "boss.zulrah")
            return rowIdx % 3 == 0 ? [questDs2] : [];
        if (actKey is "raid.cox" or "raid.toa")
            return rowIdx % 4 != 2 ? [questSote] : [];
        return [];
    }

    private static List<ActivityModifierRule> GetModifiersForTile(string actKey, int rowIdx, int tilePos, Capability questDs2, Capability itemDhl, Capability questSote)
    {
        if (rowIdx % 6 == 0 && tilePos == 0)
            return [new ActivityModifierRule(itemDhl, 0.9m, null), new ActivityModifierRule(questDs2, null, 1.1m)];
        if (rowIdx % 6 == 1 && tilePos == 1)
            return [new ActivityModifierRule(itemDhl, 0.85m, 1.05m)];
        if (actKey is "skilling.runecraft" or "skilling.mining" && rowIdx % 4 == 2)
            return [new ActivityModifierRule(questSote, 0.9m, null)];
        if (actKey is "skilling.mining" && rowIdx % 5 == 3)
            return [new ActivityModifierRule(questSote, null, 1.15m)];
        if (actKey is "raid.cox" or "raid.toa" && rowIdx % 3 == 1)
            return [new ActivityModifierRule(questSote, 0.92m, 1.08m)];
        if (actKey is "boss.zulrah" && tilePos == 2)
            return [new ActivityModifierRule(itemDhl, 0.88m, 1.05m)];
        return [];
    }

    private async Task SeedTeamsAsync(CancellationToken cancellationToken)
    {
        var playerProfiles = new List<PlayerProfile>();
        foreach (var name in SeedPlayerNames)
        {
            var p = await playerRepo.GetByNameAsync(name, cancellationToken);
            if (p is not null)
                playerProfiles.Add(p);
        }
        if (playerProfiles.Count < 4)
        {
            logger.LogWarning("Dev seed: need at least 4 players for teams; found {Count}", playerProfiles.Count);
            return;
        }

        foreach (var eventName in SeedEventNames)
        {
            var evt = await eventRepo.GetByNameAsync(eventName, cancellationToken);
            if (evt is null)
                continue;

            var existingTeams = await teamRepo.GetByEventIdAsync(evt.Id, cancellationToken);
            var teamAlpha = existingTeams.FirstOrDefault(t => t.Name == "Team Alpha");
            var teamBeta = existingTeams.FirstOrDefault(t => t.Name == "Team Beta");

            var playerIdsAlpha = playerProfiles.Take(4).Select(p => p.Id).ToList();
            var playerIdsBeta = playerProfiles.Skip(4).Take(4).Select(p => p.Id).ToList();

            if (teamAlpha is not null)
            {
                var strategyConfig = teamAlpha.StrategyConfig ?? new StrategyConfig(teamAlpha.Id, BingoSim.Application.StrategyKeys.StrategyCatalog.RowUnlocking, "{}");
                strategyConfig.Update(BingoSim.Application.StrategyKeys.StrategyCatalog.RowUnlocking, "{}");
                var teamPlayers = playerIdsAlpha.Select(pid => new TeamPlayer(teamAlpha.Id, pid)).ToList();
                await teamRepo.UpdateAsync(teamAlpha, strategyConfig, teamPlayers, cancellationToken);
                logger.LogInformation("Dev seed: updated team 'Team Alpha' for event '{EventName}'", eventName);
            }
            else
            {
                var team = new Team(evt.Id, "Team Alpha");
                var config = new StrategyConfig(team.Id, BingoSim.Application.StrategyKeys.StrategyCatalog.RowUnlocking, "{}");
                var teamPlayers = playerIdsAlpha.Select(pid => new TeamPlayer(team.Id, pid)).ToList();
                await teamRepo.AddAsync(team, config, teamPlayers, cancellationToken);
                logger.LogInformation("Dev seed: created team 'Team Alpha' for event '{EventName}'", eventName);
            }

            if (teamBeta is not null)
            {
                var strategyConfig = teamBeta.StrategyConfig ?? new StrategyConfig(teamBeta.Id, BingoSim.Application.StrategyKeys.StrategyCatalog.RowUnlocking, "{}");
                strategyConfig.Update(BingoSim.Application.StrategyKeys.StrategyCatalog.RowUnlocking, "{}");
                var teamPlayers = playerIdsBeta.Select(pid => new TeamPlayer(teamBeta.Id, pid)).ToList();
                await teamRepo.UpdateAsync(teamBeta, strategyConfig, teamPlayers, cancellationToken);
                logger.LogInformation("Dev seed: updated team 'Team Beta' for event '{EventName}'", eventName);
            }
            else
            {
                var team = new Team(evt.Id, "Team Beta");
                var config = new StrategyConfig(team.Id, BingoSim.Application.StrategyKeys.StrategyCatalog.RowUnlocking, "{}");
                var teamPlayers = playerIdsBeta.Select(pid => new TeamPlayer(team.Id, pid)).ToList();
                await teamRepo.AddAsync(team, config, teamPlayers, cancellationToken);
                logger.LogInformation("Dev seed: created team 'Team Beta' for event '{EventName}'", eventName);
            }
        }
    }

    private sealed record PlayerSeedDef(string Name, decimal SkillTimeMultiplier, List<Capability> Capabilities, WeeklySchedule WeeklySchedule);

    private sealed record ActivitySeedDef(string Key, string Name, ActivityModeSupport ModeSupport, List<ActivityAttemptDefinition> Attempts, List<GroupSizeBand> GroupScalingBands);

    private sealed record EventSeedDef(string Name, TimeSpan Duration, List<Row> Rows);
}
