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
        logger.LogInformation("Real event seed: Bingo7 — seeding activities and Rows 0–4");

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
        const string dropDkRing = "item.dk_ring";
        const string dropTdUnique = "item.td_unique";
        const string dropCerberusUnique = "item.cerberus_unique";
        const string dropVorkathItem = "item.vorkath_item";
        const string dropElderRobe = "item.elder_chaos_robe";
        const string dropZenyteShard = "item.zenyte_shard";
        const string dropGauntletSeed = "item.gauntlet_seed";
        const string dropRevTotem = "item.rev_totem";
        const string dropChampionScroll = "item.champion_scroll";
        const string dropWildernessShield = "item.wilderness_shield";
        const string dropVetionUnique = "item.vetion_unique";
        const string dropCallistoHilt = "item.callisto_hilt";
        const string dropZombieAxeHelm = "item.zombie_axe_helm";
        const string dropRoyalTitanUnique = "item.royal_titan_unique";
        const string dropYamaUnique = "item.yama_unique";
        const string dropNightmareBass = "item.nightmare_bass";

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

            // Dagannoth Kings: 1/128 ring, 1/5000 pet (counts as 3 rings), ~40s per kill, solo
            new ActivitySeedDef(
                "boss.dagannoth_kings", "Dagannoth Kings",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(40, TimeDistribution.Uniform, 8),
                        [
                            new ActivityOutcomeDefinition("nothing", 158718, 160000, []),
                            new ActivityOutcomeDefinition("ring", 1250, 160000, [new ProgressGrant(dropDkRing, 1)]),
                            new ActivityOutcomeDefinition("pet", 32, 160000, [new ProgressGrant(dropDkRing, 3)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Tormented Demons: 1/500 Synapse OR 1/501 Claw per kill (mutually exclusive), ~60s per kill, solo
            new ActivitySeedDef(
                "monster.tormented_demons", "Tormented Demons",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(60, TimeDistribution.Uniform, 12),
                        [
                            new ActivityOutcomeDefinition("nothing", 249499, 250500, []),
                            new ActivityOutcomeDefinition("synapse", 501, 250500, [new ProgressGrant(dropTdUnique, 1)]),
                            new ActivityOutcomeDefinition("claw", 500, 250500, [new ProgressGrant(dropTdUnique, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Cerberus: 4 uniques at 1/520 each (4/520 combined), pet 1/3000 (counts as 1 unique), ~69s per kill, solo
            new ActivitySeedDef(
                "boss.cerberus", "Cerberus",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(69, TimeDistribution.Uniform, 12),
                        [
                            new ActivityOutcomeDefinition("nothing", 38687, 39000, []),
                            new ActivityOutcomeDefinition("unique", 300, 39000, [new ProgressGrant(dropCerberusUnique, 1)]),
                            new ActivityOutcomeDefinition("pet", 13, 39000, [new ProgressGrant(dropCerberusUnique, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Vorkath: 2 rolls per kill. Qualifying items: head 1/50, necklace 1/1000, jar 1/3000, pet 1/3000, draconic 1/5000, skeletal 1/5000. ~132s per kill, solo.
            new ActivitySeedDef(
                "boss.vorkath", "Vorkath",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(132, TimeDistribution.Uniform, 25),
                        [
                            new ActivityOutcomeDefinition("zero", 215179561, 225000000, []),
                            new ActivityOutcomeDefinition("one", 9710878, 225000000, [new ProgressGrant(dropVorkathItem, 1)]),
                            new ActivityOutcomeDefinition("two", 109561, 225000000, [new ProgressGrant(dropVorkathItem, 2)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Elder Chaos Druid: hood/robe/top each 1/1419, one roll per kill, ~38s per kill, solo
            new ActivitySeedDef(
                "monster.elder_chaos_druid", "Elder Chaos Druid",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(38, TimeDistribution.Uniform, 8),
                        [
                            new ActivityOutcomeDefinition("nothing", 1416, 1419, []),
                            new ActivityOutcomeDefinition("piece", 3, 1419, [new ProgressGrant(dropElderRobe, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Demonic Gorilla: 1/300 Zenyte shard, ~60s per kill, solo
            new ActivitySeedDef(
                "monster.demonic_gorilla", "Demonic Gorilla",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(60, TimeDistribution.Uniform, 12),
                        [
                            new ActivityOutcomeDefinition("nothing", 299, 300, []),
                            new ActivityOutcomeDefinition("shard", 1, 300, [new ProgressGrant(dropZenyteShard, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // The Gauntlet: 3 rolls per completion. Weapon/armor 1/120 each (1 seed), enhanced/pet 1/2000 each (3 seeds). ~12 min per completion, solo.
            new ActivitySeedDef(
                "minigame.gauntlet", "The Gauntlet",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("completion", RollScope.PerPlayer, new AttemptTimeModel(720, TimeDistribution.Uniform, 120),
                        [
                            new ActivityOutcomeDefinition("zero", 9494, 10000, []),
                            new ActivityOutcomeDefinition("one", 460, 10000, [new ProgressGrant(dropGauntletSeed, 1)]),
                            new ActivityOutcomeDefinition("two", 10, 10000, [new ProgressGrant(dropGauntletSeed, 2)]),
                            new ActivityOutcomeDefinition("three", 30, 10000, [new ProgressGrant(dropGauntletSeed, 3)]),
                            new ActivityOutcomeDefinition("four", 5, 10000, [new ProgressGrant(dropGauntletSeed, 4)]),
                            new ActivityOutcomeDefinition("five", 1, 10000, [new ProgressGrant(dropGauntletSeed, 5)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Revenant Knight: 6 totem items. P(any) = 1/4400+1/1100+1/2200+3/4400 = 10/4400 = 1/440. One drop per kill, ~29s per kill, solo.
            new ActivitySeedDef(
                "monster.revenant_knight", "Revenant Knight",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(29, TimeDistribution.Uniform, 6),
                        [
                            new ActivityOutcomeDefinition("nothing", 4390, 4400, []),
                            new ActivityOutcomeDefinition("totem", 10, 4400, [new ProgressGrant(dropRevTotem, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Champion scroll: 3 generic scrolls at 1/5000 each, ~4.6s per kill, solo
            new ActivitySeedDef(
                "monster.champion_scroll", "Champion Scroll",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(5, TimeDistribution.Uniform, 2),
                        [
                            new ActivityOutcomeDefinition("nothing", 4999, 5000, []),
                            new ActivityOutcomeDefinition("scroll", 1, 5000, [new ProgressGrant(dropChampionScroll, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Chaos Fanatic: 2 shards at 1/256 each, pet 1/1000 (counts as 2), ~60s per kill, solo
            new ActivitySeedDef(
                "boss.chaos_fanatic", "Chaos Fanatic",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(60, TimeDistribution.Uniform, 12),
                        [
                            new ActivityOutcomeDefinition("nothing", 15859, 16000, []),
                            new ActivityOutcomeDefinition("shard", 125, 16000, [new ProgressGrant(dropWildernessShield, 1)]),
                            new ActivityOutcomeDefinition("pet", 16, 16000, [new ProgressGrant(dropWildernessShield, 2)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Crazy Archaeologist: 2 shards at 1/256 each, ~60s per kill, solo
            new ActivitySeedDef(
                "boss.crazy_archaeologist", "Crazy Archaeologist",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(60, TimeDistribution.Uniform, 12),
                        [
                            new ActivityOutcomeDefinition("nothing", 127, 128, []),
                            new ActivityOutcomeDefinition("shard", 1, 128, [new ProgressGrant(dropWildernessShield, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Scorpia: 2 shards at 1/256 each, pet 1/2016 (counts as 2), ~90s per kill, solo
            new ActivitySeedDef(
                "boss.scorpia", "Scorpia",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(90, TimeDistribution.Uniform, 18),
                        [
                            new ActivityOutcomeDefinition("nothing", 15994, 16128, []),
                            new ActivityOutcomeDefinition("shard", 126, 16128, [new ProgressGrant(dropWildernessShield, 1)]),
                            new ActivityOutcomeDefinition("pet", 8, 16128, [new ProgressGrant(dropWildernessShield, 2)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Vet'ion: voidwaker blade 1/360, ring of the gods 1/512, pet 1/1500 (counts as 1), ~62s per kill, solo
            new ActivitySeedDef(
                "boss.vetion", "Vet'ion",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(62, TimeDistribution.Uniform, 12),
                        [
                            new ActivityOutcomeDefinition("nothing", 572891, 576000, []),
                            new ActivityOutcomeDefinition("blade", 1600, 576000, [new ProgressGrant(dropVetionUnique, 1)]),
                            new ActivityOutcomeDefinition("ring", 1125, 576000, [new ProgressGrant(dropVetionUnique, 1)]),
                            new ActivityOutcomeDefinition("pet", 384, 576000, [new ProgressGrant(dropVetionUnique, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Callisto: voidwaker hilt 1/360, pet 1/1500 (counts as 1), ~83s per kill, solo
            new ActivitySeedDef(
                "boss.callisto", "Callisto",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(83, TimeDistribution.Uniform, 16),
                        [
                            new ActivityOutcomeDefinition("nothing", 8969, 9000, []),
                            new ActivityOutcomeDefinition("hilt", 25, 9000, [new ProgressGrant(dropCallistoHilt, 1)]),
                            new ActivityOutcomeDefinition("pet", 6, 9000, [new ProgressGrant(dropCallistoHilt, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Armoured Zombie (Varrock): 1/600 axe or helm, ~4.5s per kill, solo. Requires Defender of Varrock.
            new ActivitySeedDef(
                "monster.armoured_zombie_varrock", "Armoured Zombie (Varrock)",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(5, TimeDistribution.Uniform, 2),
                        [
                            new ActivityOutcomeDefinition("nothing", 599, 600, []),
                            new ActivityOutcomeDefinition("drop", 1, 600, [new ProgressGrant(dropZombieAxeHelm, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Armoured Zombie (Arrav): 1/800 axe or helm, ~4.5s per kill, solo. Requires Curse of Arrav.
            new ActivitySeedDef(
                "monster.armoured_zombie_arrav", "Armoured Zombie (Arrav)",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(5, TimeDistribution.Uniform, 2),
                        [
                            new ActivityOutcomeDefinition("nothing", 799, 800, []),
                            new ActivityOutcomeDefinition("drop", 1, 800, [new ProgressGrant(dropZombieAxeHelm, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Royal Titans: 2 uniques at 1/75 each, solo or duo. Duo twice as fast. ~480s solo, ~240s duo. PerGroup (one roll per kill).
            new ActivitySeedDef(
                "boss.royal_titans", "Royal Titans",
                new ActivityModeSupport(true, true, 1, 2),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerGroup, new AttemptTimeModel(480, TimeDistribution.Uniform, 100),
                        [
                            new ActivityOutcomeDefinition("nothing", 73, 75, []),
                            new ActivityOutcomeDefinition("unique", 2, 75, [new ProgressGrant(dropRoyalTitanUnique, 1)]),
                        ]),
                ],
                [
                    new GroupSizeBand(1, 1, 1.0m, 1.0m),   // solo: 480s
                    new GroupSizeBand(2, 2, 0.5m, 1.0m),   // duo: 240s
                ]),

            // Yama: 1 unique (horn 1/300, armor 3×1/600). Shards dry protection not modeled. ~360s solo, ~180s duo. Requires A Kingdom Divided.
            new ActivitySeedDef(
                "boss.yama", "Yama",
                new ActivityModeSupport(true, true, 1, 2),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerGroup, new AttemptTimeModel(360, TimeDistribution.Uniform, 60),
                        [
                            new ActivityOutcomeDefinition("nothing", 119, 120, []),
                            new ActivityOutcomeDefinition("unique", 1, 120, [new ProgressGrant(dropYamaUnique, 1)]),
                        ]),
                ],
                [
                    new GroupSizeBand(1, 1, 1.0m, 1.0m),   // solo: 360s
                    new GroupSizeBand(2, 2, 0.5m, 1.0m),   // duo: 180s
                ]),

            // The Nightmare (group): bass 1/17, unique = 10 bass. 3-12 players. Kill time varies: 3-4p ~8min, 5-9p ~9min, 10+p ~6min (with buffer).
            new ActivitySeedDef(
                "boss.nightmare", "The Nightmare",
                new ActivityModeSupport(false, true, 3, 12),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerGroup, new AttemptTimeModel(480, TimeDistribution.Uniform, 120),
                        [
                            new ActivityOutcomeDefinition("nothing", 991, 1071, []),
                            new ActivityOutcomeDefinition("bass", 63, 1071, [new ProgressGrant(dropNightmareBass, 1)]),
                            new ActivityOutcomeDefinition("unique", 17, 1071, [new ProgressGrant(dropNightmareBass, 10)]),
                        ]),
                ],
                [
                    new GroupSizeBand(3, 4, 1.0m, 1.0m),   // 6-7 min + buffer ~8 min
                    new GroupSizeBand(5, 9, 1.2m, 1.0m),   // 7-9 min + buffer ~10 min
                    new GroupSizeBand(10, 12, 0.82m, 1.0m), // <5 min + buffer ~6 min
                ]),

            // Phosani's Nightmare (solo): bass 1/17, unique = 10 bass. Lower unique rates. 8-10 min + buffer.
            new ActivitySeedDef(
                "boss.phosani_nightmare", "Phosani's Nightmare",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(630, TimeDistribution.Uniform, 90),
                        [
                            new ActivityOutcomeDefinition("nothing", 9410, 10000, []),
                            new ActivityOutcomeDefinition("bass", 580, 10000, [new ProgressGrant(dropNightmareBass, 1)]),
                            new ActivityOutcomeDefinition("unique", 10, 10000, [new ProgressGrant(dropNightmareBass, 10)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),
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
        var dkId = activityIdsByKey["boss.dagannoth_kings"];
        var dkKey = "boss.dagannoth_kings";
        var tdId = activityIdsByKey["monster.tormented_demons"];
        var tdKey = "monster.tormented_demons";
        var cerberusId = activityIdsByKey["boss.cerberus"];
        var cerberusKey = "boss.cerberus";
        var vorkathId = activityIdsByKey["boss.vorkath"];
        var vorkathKey = "boss.vorkath";
        var elderDruidId = activityIdsByKey["monster.elder_chaos_druid"];
        var elderDruidKey = "monster.elder_chaos_druid";
        var demonicGorillaId = activityIdsByKey["monster.demonic_gorilla"];
        var demonicGorillaKey = "monster.demonic_gorilla";
        var gauntletId = activityIdsByKey["minigame.gauntlet"];
        var gauntletKey = "minigame.gauntlet";
        var revKnightId = activityIdsByKey["monster.revenant_knight"];
        var revKnightKey = "monster.revenant_knight";
        var championScrollId = activityIdsByKey["monster.champion_scroll"];
        var championScrollKey = "monster.champion_scroll";
        var chaosFanaticId = activityIdsByKey["boss.chaos_fanatic"];
        var chaosFanaticKey = "boss.chaos_fanatic";
        var crazyArchId = activityIdsByKey["boss.crazy_archaeologist"];
        var crazyArchKey = "boss.crazy_archaeologist";
        var scorpiaId = activityIdsByKey["boss.scorpia"];
        var scorpiaKey = "boss.scorpia";
        var vetionId = activityIdsByKey["boss.vetion"];
        var vetionKey = "boss.vetion";
        var callistoId = activityIdsByKey["boss.callisto"];
        var callistoKey = "boss.callisto";
        var armouredZombieVarrockId = activityIdsByKey["monster.armoured_zombie_varrock"];
        var armouredZombieVarrockKey = "monster.armoured_zombie_varrock";
        var armouredZombieArravId = activityIdsByKey["monster.armoured_zombie_arrav"];
        var armouredZombieArravKey = "monster.armoured_zombie_arrav";
        var royalTitansId = activityIdsByKey["boss.royal_titans"];
        var royalTitansKey = "boss.royal_titans";
        var yamaId = activityIdsByKey["boss.yama"];
        var yamaKey = "boss.yama";
        var nightmareId = activityIdsByKey["boss.nightmare"];
        var nightmareKey = "boss.nightmare";
        var phosaniId = activityIdsByKey["boss.phosani_nightmare"];
        var phosaniKey = "boss.phosani_nightmare";

        // Capabilities for tile requirements (players must have these to attempt)
        var slayer51 = new Capability("slayer.51", "Slayer 51");
        var raidCox = new Capability("raid.cox", "Chambers of Xeric");
        var questWgs = new Capability("quest.wgs", "While Guthix Sleeps");
        var slayer91 = new Capability("slayer.91", "Slayer 91");
        var questDs2 = new Capability("quest.ds2", "Dragon Slayer II");
        var questMm2 = new Capability("quest.mm2", "Monkey Madness II");
        var questSote = new Capability("quest.sote", "Song of the Elves");
        var questCurseOfArrav = new Capability("quest.curse_of_arrav", "The Curse of Arrav");
        var questDefenderOfVarrock = new Capability("quest.defender_of_varrock", "Defender of Varrock");
        var questKingdomDivided = new Capability("quest.kingdom_divided", "A Kingdom Divided");

        var row0 = new Row(0,
        [
            new Tile("t1-r0", "3x Merfolk Trident", 1, 3,
                [new TileActivityRule(underwaterId, underwaterKey, ["item.merfolk_trident"], [], [])]),
            new Tile("t2-r0", "2x Belle's Folly", 2, 2,
                [new TileActivityRule(gryphonId, gryphonKey, ["item.belles_folly"], [slayer51], [])]),
            new Tile("t3-r0", "1x Dragon Warhammer", 3, 1,
                [new TileActivityRule(shamanId, shamanKey, ["item.dragon_warhammer"], [], [])]),
            new Tile("t4-r0", "800 Dragon Arrows", 4, 800,
                [new TileActivityRule(coxId, coxKey, ["loot.dragon_arrows"], [raidCox], [])]),
        ]);

        var row1 = new Row(1,
        [
            new Tile("t1-r1", "6x DK Ring", 1, 6,
                [new TileActivityRule(dkId, dkKey, ["item.dk_ring"], [], [])]),
            new Tile("t2-r1", "3x TD Unique", 2, 3,
                [new TileActivityRule(tdId, tdKey, ["item.td_unique"], [questWgs], [])]),
            new Tile("t3-r1", "5x Cerberus Unique", 3, 5,
                [new TileActivityRule(cerberusId, cerberusKey, ["item.cerberus_unique"], [slayer91], [])]),
            new Tile("t4-r1", "12x Vorkath Item", 4, 12,
                [new TileActivityRule(vorkathId, vorkathKey, ["item.vorkath_item"], [questDs2], [])]),
        ]);

        var row2 = new Row(2,
        [
            new Tile("t1-r2", "2x Elder Chaos Druid Robes", 1, 2,
                [new TileActivityRule(elderDruidId, elderDruidKey, ["item.elder_chaos_robe"], [], [])]),
            new Tile("t2-r2", "3x Zenyte Shard", 2, 3,
                [new TileActivityRule(demonicGorillaId, demonicGorillaKey, ["item.zenyte_shard"], [questMm2], [])]),
            new Tile("t3-r2", "5x Gauntlet Seeds", 3, 5,
                [new TileActivityRule(gauntletId, gauntletKey, ["item.gauntlet_seed"], [questSote], [])]),
            new Tile("t4-r2", "5x Rev Totems", 4, 5,
                [new TileActivityRule(revKnightId, revKnightKey, ["item.rev_totem"], [], [])]),
        ]);

        var row3 = new Row(3,
        [
            new Tile("t1-r3", "1x Champion Scroll Set", 1, 3,
                [new TileActivityRule(championScrollId, championScrollKey, ["item.champion_scroll"], [], [])]),
            new Tile("t2-r3", "1x Wilderness Shield Set", 2, 6,
                [
                    new TileActivityRule(chaosFanaticId, chaosFanaticKey, ["item.wilderness_shield"], [], []),
                    new TileActivityRule(crazyArchId, crazyArchKey, ["item.wilderness_shield"], [], []),
                    new TileActivityRule(scorpiaId, scorpiaKey, ["item.wilderness_shield"], [], []),
                ]),
            new Tile("t3-r3", "6x Vet'ion Unique", 3, 6,
                [new TileActivityRule(vetionId, vetionKey, ["item.vetion_unique"], [], [])]),
            new Tile("t4-r3", "3x Callisto Hilt", 4, 3,
                [new TileActivityRule(callistoId, callistoKey, ["item.callisto_hilt"], [], [])]),
        ]);

        var row4 = new Row(4,
        [
            new Tile("t1-r4", "12x Zombie Axe or Helm", 1, 12,
                [
                    new TileActivityRule(armouredZombieVarrockId, armouredZombieVarrockKey, ["item.zombie_axe_helm"], [questDefenderOfVarrock], []),
                    new TileActivityRule(armouredZombieArravId, armouredZombieArravKey, ["item.zombie_axe_helm"], [questCurseOfArrav], []),
                ]),
            new Tile("t2-r4", "6x Royal Titan Unique", 2, 6,
                [new TileActivityRule(royalTitansId, royalTitansKey, ["item.royal_titan_unique"], [], [])]),
            new Tile("t3-r4", "1x Yama Unique", 3, 1,
                [new TileActivityRule(yamaId, yamaKey, ["item.yama_unique"], [questKingdomDivided], [])]),
            new Tile("t4-r4", "25x Nightmare Bass", 4, 25,
                [
                    new TileActivityRule(nightmareId, nightmareKey, ["item.nightmare_bass"], [], []),
                    new TileActivityRule(phosaniId, phosaniKey, ["item.nightmare_bass"], [], []),
                ]),
        ]);

        var existing = await _eventRepo.GetByNameAsync(eventName, cancellationToken);

        if (existing is not null)
        {
            existing.UpdateDuration(duration);
            existing.SetUnlockPointsRequiredPerRow(unlockPointsPerRow);
            existing.SetRows([row0, row1, row2, row3, row4]);
            await _eventRepo.UpdateAsync(existing, cancellationToken);
            logger.LogInformation("Real event seed: updated event '{Name}'", eventName);
        }
        else
        {
            var evt = new Event(eventName, duration, unlockPointsPerRow);
            evt.SetRows([row0, row1, row2, row3, row4]);
            await _eventRepo.AddAsync(evt, cancellationToken);
            logger.LogInformation("Real event seed: created event '{Name}'", eventName);
        }
    }

    private sealed record ActivitySeedDef(string Key, string Name, ActivityModeSupport ModeSupport, List<ActivityAttemptDefinition> Attempts, List<GroupSizeBand> GroupScalingBands);
}
