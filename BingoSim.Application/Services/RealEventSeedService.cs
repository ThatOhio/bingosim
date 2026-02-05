using BingoSim.Application.Interfaces;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.Interfaces;
using BingoSim.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BingoSim.Application.Services;

/// <summary>
/// Seeds real-world event data. Each event (e.g., Bingo7) is built out progressively
/// as event data is provided. Do not fill in gaps—ask for missing information.
/// </summary>
public class RealEventSeedService(
    IPlayerProfileRepository _playerRepo,
    IActivityDefinitionRepository _activityRepo,
    IEventRepository _eventRepo,
    ITeamRepository _teamRepo,
    ILogger<RealEventSeedService> logger) : IRealEventSeedService
{
    public async Task SeedEventAsync(string eventKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
            throw new ArgumentException("Event key cannot be null or empty.", nameof(eventKey));

        var normalized = eventKey.Trim();

        switch (normalized)
        {
            case "Bingo7":
                await SeedBingo7Async(cancellationToken);
                break;
            default:
                throw new ArgumentException($"Unknown real event: '{normalized}'. Supported: Bingo7.", nameof(eventKey));
        }
    }

    /// <summary>
    /// Bingo7 real event. Build out as event data is provided—do not fill gaps.
    /// </summary>
    private async Task SeedBingo7Async(CancellationToken cancellationToken)
    {
        logger.LogInformation("Real event seed: Bingo7 — seeding activities and Row 0");

        var activityIdsByKey = await SeedBingo7ActivitiesAsync(cancellationToken);
        await SeedBingo7EventAsync(activityIdsByKey, cancellationToken);

        logger.LogInformation("Real event seed: Bingo7 — done");
    }

    private async Task<Dictionary<string, Guid>> SeedBingo7ActivitiesAsync(CancellationToken cancellationToken)
    {
        var definitions = GetBingo7ActivityDefinitions();
        var idsByKey = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (var def in definitions)
        {
            var existing = await _activityRepo.GetByKeyAsync(def.Key, cancellationToken);

            if (existing is not null)
            {
                existing.UpdateName(def.Name);
                existing.SetModeSupport(def.ModeSupport);
                existing.SetAttempts(def.Attempts);
                existing.SetGroupScalingBands(def.GroupScalingBands);
                await _activityRepo.UpdateAsync(existing, cancellationToken);
                idsByKey[def.Key] = existing.Id;
                logger.LogInformation("Real event seed: updated activity '{Key}'", def.Key);
            }
            else
            {
                var activity = new ActivityDefinition(def.Key, def.Name, def.ModeSupport);
                activity.SetAttempts(def.Attempts);
                activity.SetGroupScalingBands(def.GroupScalingBands);
                await _activityRepo.AddAsync(activity, cancellationToken);
                idsByKey[def.Key] = activity.Id;
                logger.LogInformation("Real event seed: created activity '{Key}'", def.Key);
            }
        }

        return idsByKey;
    }

    private static List<ActivitySeedDef> GetBingo7ActivityDefinitions()
    {
        const string dropMerfolkTrident = "item.merfolk_trident";
        const string dropBellesFolly = "item.belles_folly";
        const string dropDragonWarhammer = "item.dragon_warhammer";
        const string dropDragonArrows = "loot.dragon_arrows";

        return
        [
            // Underwater Agility: deterministic 2h per trident, 1/1
            new ActivitySeedDef(
                "minigame.underwater_agility", "Underwater Agility",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("trident", RollScope.PerPlayer, new AttemptTimeModel(7200, TimeDistribution.Uniform, 0),
                        [new ActivityOutcomeDefinition("trident", 1, 1, [new ProgressGrant(dropMerfolkTrident, 1)])]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Shellbane gryphon: 1/256 Belle's folly, 1/3000 pet (counts as 1 folly), ~91s per kill
            new ActivitySeedDef(
                "boss.shellbane_gryphon", "Shellbane Gryphon",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(91, TimeDistribution.Uniform, 15),
                        [
                            new ActivityOutcomeDefinition("nothing", 191186, 192000, []),
                            new ActivityOutcomeDefinition("folly", 750, 192000, [new ProgressGrant(dropBellesFolly, 1)]),
                            new ActivityOutcomeDefinition("pet", 64, 192000, [new ProgressGrant(dropBellesFolly, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Lizardman shaman: 1/3000 dragon warhammer, ~30s per kill
            new ActivitySeedDef(
                "monster.lizardman_shaman", "Lizardman Shaman",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(30, TimeDistribution.Uniform, 5),
                        [new ActivityOutcomeDefinition("common", 2999, 3000, []), new ActivityOutcomeDefinition("rare", 1, 3000, [new ProgressGrant(dropDragonWarhammer, 1)])]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Chambers of Xeric: dragon arrows. 30 min per raid, PerPlayer rolls.
            // Purple 1/20: 52/53 → 250 arrows; 1/53 → pet (additional 250) = 500 total. No purple: 2 rolls at 1/33 each → 30-200 or 60-400.
            new ActivitySeedDef(
                "raid.cox", "Chambers of Xeric",
                new ActivityModeSupport(true, true, 1, 8),
                [
                    new ActivityAttemptDefinition("completion", RollScope.PerPlayer, new AttemptTimeModel(1800, TimeDistribution.Uniform, 300),
                        [
                            new ActivityOutcomeDefinition("purple_no_pet", 56628, 1154340, [new ProgressGrant(dropDragonArrows, 250)]),
                            new ActivityOutcomeDefinition("purple_with_pet", 1089, 1154340, [new ProgressGrant(dropDragonArrows, 500)]),
                            new ActivityOutcomeDefinition("zero_arrows", 1031168, 1154340, []),
                            new ActivityOutcomeDefinition("one_roll", 64448, 1154340, [new ProgressGrant(dropDragonArrows, 30, 200)]),
                            new ActivityOutcomeDefinition("two_rolls", 1007, 1154340, [new ProgressGrant(dropDragonArrows, 60, 400)]),
                        ]),
                ],
                [
                    new GroupSizeBand(1, 8, 1.0m, 1.0m), // 1/20 purple for all; solo 1/30 approximated as 1/20 for simplicity
                ]),
        ];
    }

    private async Task SeedBingo7EventAsync(Dictionary<string, Guid> activityIdsByKey, CancellationToken cancellationToken)
    {
        const string eventName = "Bingo7";
        var duration = TimeSpan.FromHours(9 * 24); // 9 days
        const int unlockPointsPerRow = 5;

        var coxId = activityIdsByKey["raid.cox"];
        var coxKey = "raid.cox";
        var underwaterId = activityIdsByKey["minigame.underwater_agility"];
        var underwaterKey = "minigame.underwater_agility";
        var gryphonId = activityIdsByKey["boss.shellbane_gryphon"];
        var gryphonKey = "boss.shellbane_gryphon";
        var shamanId = activityIdsByKey["monster.lizardman_shaman"];
        var shamanKey = "monster.lizardman_shaman";

        var row0 = new Row(0,
        [
            new Tile("t1-r0", "3x Merfolk Trident", 1, 3,
                [new TileActivityRule(underwaterId, underwaterKey, ["item.merfolk_trident"], [], [])]),
            new Tile("t2-r0", "2x Belle's Folly", 2, 2,
                [new TileActivityRule(gryphonId, gryphonKey, ["item.belles_folly"], [], [])]),
            new Tile("t3-r0", "1x Dragon Warhammer", 3, 1,
                [new TileActivityRule(shamanId, shamanKey, ["item.dragon_warhammer"], [], [])]),
            new Tile("t4-r0", "800 Dragon Arrows", 4, 800,
                [new TileActivityRule(coxId, coxKey, ["loot.dragon_arrows"], [], [])]),
        ]);

        var existing = await _eventRepo.GetByNameAsync(eventName, cancellationToken);

        if (existing is not null)
        {
            existing.UpdateDuration(duration);
            existing.SetUnlockPointsRequiredPerRow(unlockPointsPerRow);
            existing.SetRows([row0]);
            await _eventRepo.UpdateAsync(existing, cancellationToken);
            logger.LogInformation("Real event seed: updated event '{Name}'", eventName);
        }
        else
        {
            var evt = new Event(eventName, duration, unlockPointsPerRow);
            evt.SetRows([row0]);
            await _eventRepo.AddAsync(evt, cancellationToken);
            logger.LogInformation("Real event seed: created event '{Name}'", eventName);
        }
    }

    private sealed record ActivitySeedDef(string Key, string Name, ActivityModeSupport ModeSupport, List<ActivityAttemptDefinition> Attempts, List<GroupSizeBand> GroupScalingBands);
}
