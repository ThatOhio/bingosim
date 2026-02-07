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
        logger.LogInformation("Real event seed: Bingo7 — seeding activities and Rows 0–14");

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
        const string dropSlayerItem = "item.slayer_item";
        const string dropBarrowsUnique = "item.barrows_unique";
        const string dropDoomUnique = "item.doom_unique";
        const string dropAraxxorUnique = "item.araxxor_unique";
        const string dropMoonsUnique = "item.moons_unique";
        const string dropGraardorUnique = "item.graardor_unique";
        const string dropBottledStorm = "item.bottled_storm";
        const string dropSwiftAlbatrossFeather = "item.swift_albatross_feather";
        const string dropMonkeyLap = "progress.monkey_lap";
        const string dropSailingTreasureUnique = "item.sailing_treasure_unique";
        const string dropDukeUnique = "item.duke_unique";
        const string dropTobVials = "loot.tob_vials";
        const string dropSharkPaint = "item.shark_paint";
        const string dropZulrahUnique = "item.zulrah_unique";
        const string dropHookOrBarbs = "item.hook_or_barbs";
        const string dropRaidCosmetic = "loot.raid_cosmetic";
        const string dropFedora = "item.fedora";
        const string dropZilyanaUnique = "item.zilyana_unique";
        const string dropNightmareR9 = "item.nightmare_r9";
        const string dropSoakedPages = "item.soaked_pages";
        const string dropVenatorShard = "item.venator_shard";
        const string dropColosseumUnique = "item.colosseum_unique";
        const string dropNexTorvaOrShards = "item.nex_torva_or_shards";
        const string dropAbyssalDye = "item.abyssal_dye";
        const string dropBarracudaPaint = "item.barracuda_paint";
        const string dropWhispererUnique = "item.whisperer_unique";
        const string dropRevWeaponPoints = "item.rev_weapon_points";
        const string dropBrineSabre = "item.brine_sabre";
        const string dropGrotesqueUnique = "item.grotesque_unique";
        const string dropToaBattlestaff = "loot.toa_battlestaff";
        const string dropLeviathanUnique = "item.leviathan_unique";
        const string dropPharaohSceptre = "item.pharaoh_sceptre";
        const string dropAbyssalSireUnique = "item.abyssal_sire_unique";
        const string dropNexUniqueOrShardsR13 = "item.nex_unique_or_shards_r13";
        const string dropScurriusSpine = "item.scurrius_spine";
        const string dropDragonMetalSheet = "loot.dragon_metal_sheet";

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

            // Chambers of Xeric: dragon arrows + cosmetic (dust 1/400, kit 1/75, pet 1/1060). 30 min per raid, PerPlayer.
            // Purple 1/20: 52/53 → 250 arrows; 1/53 → pet (500 arrows + 1 cosmetic). No purple: 2 rolls at 1/33 each.
            // Cosmetic: dust 1/400, twisted ancestral 1/75 (per completion); pet grants cosmetic on purple.
            new ActivitySeedDef(
                "raid.cox", "Chambers of Xeric",
                new ActivityModeSupport(true, true, 1, 8),
                [
                    new ActivityAttemptDefinition("completion", RollScope.PerPlayer, new AttemptTimeModel(1800, TimeDistribution.Uniform, 300),
                        [
                            new ActivityOutcomeDefinition("purple_no_pet", 55732, 1154340, [new ProgressGrant(dropDragonArrows, 250)]),
                            new ActivityOutcomeDefinition("purple_no_pet_dust", 141, 1154340, [new ProgressGrant(dropDragonArrows, 650), new ProgressGrant(dropRaidCosmetic, 1)]),
                            new ActivityOutcomeDefinition("purple_no_pet_kit", 755, 1154340, [new ProgressGrant(dropDragonArrows, 650), new ProgressGrant(dropRaidCosmetic, 1)]),
                            new ActivityOutcomeDefinition("purple_with_pet", 1089, 1154340, [new ProgressGrant(dropDragonArrows, 900), new ProgressGrant(dropRaidCosmetic, 1)]),
                            new ActivityOutcomeDefinition("zero_arrows", 1014841, 1154340, []),
                            new ActivityOutcomeDefinition("zero_arrows_dust", 2578, 1154340, [new ProgressGrant(dropDragonArrows, 400), new ProgressGrant(dropRaidCosmetic, 1)]),
                            new ActivityOutcomeDefinition("zero_arrows_kit", 13749, 1154340, [new ProgressGrant(dropDragonArrows, 400), new ProgressGrant(dropRaidCosmetic, 1)]),
                            new ActivityOutcomeDefinition("one_roll", 63428, 1154340, [new ProgressGrant(dropDragonArrows, 30, 200)]),
                            new ActivityOutcomeDefinition("one_roll_dust", 161, 1154340, [new ProgressGrant(dropDragonArrows, 430, 600), new ProgressGrant(dropRaidCosmetic, 1)]),
                            new ActivityOutcomeDefinition("one_roll_kit", 859, 1154340, [new ProgressGrant(dropDragonArrows, 430, 600), new ProgressGrant(dropRaidCosmetic, 1)]),
                            new ActivityOutcomeDefinition("two_rolls", 992, 1154340, [new ProgressGrant(dropDragonArrows, 60, 400)]),
                            new ActivityOutcomeDefinition("two_rolls_dust", 2, 1154340, [new ProgressGrant(dropDragonArrows, 460, 800), new ProgressGrant(dropRaidCosmetic, 1)]),
                            new ActivityOutcomeDefinition("two_rolls_kit", 13, 1154340, [new ProgressGrant(dropDragonArrows, 460, 800), new ProgressGrant(dropRaidCosmetic, 1)]),
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

            // Revenant Knight: totem 1/440 (row 2), weapon table: amulet 1/1467 (1 pt), Craw's/Thammaron's/Viggora's 3/2933 (2 pts each). ~29s per kill, solo.
            new ActivitySeedDef(
                "monster.revenant_knight", "Revenant Knight",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(29, TimeDistribution.Uniform, 6),
                        [
                            new ActivityOutcomeDefinition("nothing", 4383, 4400, []),
                            new ActivityOutcomeDefinition("totem", 10, 4400, [new ProgressGrant(dropRevTotem, 1)]),
                            new ActivityOutcomeDefinition("amulet", 3, 4400, [new ProgressGrant(dropRevWeaponPoints, 1)]),
                            new ActivityOutcomeDefinition("weapon", 4, 4400, [new ProgressGrant(dropRevWeaponPoints, 2)]),
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

            // Crazy Archaeologist: Fedora 1/128, shield shards 2×1/256 (1/128 combined). One roll per kill. ~51s per kill, solo.
            new ActivitySeedDef(
                "boss.crazy_archaeologist", "Crazy Archaeologist",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(51, TimeDistribution.Uniform, 10),
                        [
                            new ActivityOutcomeDefinition("nothing", 126, 128, []),
                            new ActivityOutcomeDefinition("fedora", 1, 128, [new ProgressGrant(dropFedora, 1)]),
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

            // The Nightmare (group): bass 1/17, unique = 10 bass (row 4) or 30 progress (row 9). 3-12 players. Kill time varies.
            new ActivitySeedDef(
                "boss.nightmare", "The Nightmare",
                new ActivityModeSupport(false, true, 3, 12),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerGroup, new AttemptTimeModel(480, TimeDistribution.Uniform, 120),
                        [
                            new ActivityOutcomeDefinition("nothing", 991, 1071, []),
                            new ActivityOutcomeDefinition("bass", 63, 1071, [new ProgressGrant(dropNightmareBass, 1), new ProgressGrant(dropNightmareR9, 1)]),
                            new ActivityOutcomeDefinition("unique", 17, 1071, [new ProgressGrant(dropNightmareBass, 10), new ProgressGrant(dropNightmareR9, 30)]),
                        ]),
                ],
                [
                    new GroupSizeBand(3, 4, 1.0m, 1.0m),   // 6-7 min + buffer ~8 min
                    new GroupSizeBand(5, 9, 1.2m, 1.0m),   // 7-9 min + buffer ~10 min
                    new GroupSizeBand(10, 12, 0.82m, 1.0m), // <5 min + buffer ~6 min
                ]),

            // Phosani's Nightmare (solo): bass 1/17, unique = 10 bass (row 4) or 30 progress (row 9). 8-10 min + buffer.
            new ActivitySeedDef(
                "boss.phosani_nightmare", "Phosani's Nightmare",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(630, TimeDistribution.Uniform, 90),
                        [
                            new ActivityOutcomeDefinition("nothing", 9410, 10000, []),
                            new ActivityOutcomeDefinition("bass", 580, 10000, [new ProgressGrant(dropNightmareBass, 1), new ProgressGrant(dropNightmareR9, 1)]),
                            new ActivityOutcomeDefinition("unique", 10, 10000, [new ProgressGrant(dropNightmareBass, 10), new ProgressGrant(dropNightmareR9, 30)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Slayer: abstract 15 slayer items at 1/200 per kill, ~15s per kill, solo
            new ActivitySeedDef(
                "activity.slayer", "Slayer",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(15, TimeDistribution.Uniform, 4),
                        [
                            new ActivityOutcomeDefinition("nothing", 199, 200, []),
                            new ActivityOutcomeDefinition("item", 1, 200, [new ProgressGrant(dropSlayerItem, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Barrows: 7 rolls per completion, 1/102 per roll. ~5 min per run. Can get 0, 1, 2+ items per chest.
            new ActivitySeedDef(
                "boss.barrows", "Barrows",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("chest", RollScope.PerPlayer, new AttemptTimeModel(308, TimeDistribution.Uniform, 60),
                        [
                            new ActivityOutcomeDefinition("zero", 9340, 10000, []),
                            new ActivityOutcomeDefinition("one", 640, 10000, [new ProgressGrant(dropBarrowsUnique, 1)]),
                            new ActivityOutcomeDefinition("two", 20, 10000, [new ProgressGrant(dropBarrowsUnique, 2)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Doom of Mokhaiotl: 1/89 unique, pet 1/1000 (counts as 1), ~331s per kill, solo. Requires The Final Dawn.
            new ActivitySeedDef(
                "boss.doom_of_mokhaiotl", "Doom of Mokhaiotl",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(331, TimeDistribution.Uniform, 60),
                        [
                            new ActivityOutcomeDefinition("nothing", 87911, 89000, []),
                            new ActivityOutcomeDefinition("unique", 1000, 89000, [new ProgressGrant(dropDoomUnique, 1)]),
                            new ActivityOutcomeDefinition("pet", 89, 89000, [new ProgressGrant(dropDoomUnique, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Araxxor: 1/150 unique, pet 1/3000 (counts as 1), ~101s per kill, solo
            new ActivitySeedDef(
                "boss.araxxor", "Araxxor",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(101, TimeDistribution.Uniform, 25),
                        [
                            new ActivityOutcomeDefinition("nothing", 2070, 2100, []),
                            new ActivityOutcomeDefinition("unique", 14, 2100, [new ProgressGrant(dropAraxxorUnique, 1)]),
                            new ActivityOutcomeDefinition("pet", 1, 2100, [new ProgressGrant(dropAraxxorUnique, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Moons of Peril: 6 rolls per kill at 1/224 each, ~193s per kill, solo. Requires Perilous Moons.
            new ActivitySeedDef(
                "boss.moons_of_peril", "Moons of Peril",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(193, TimeDistribution.Uniform, 40),
                        [
                            new ActivityOutcomeDefinition("zero", 9736, 10000, []),
                            new ActivityOutcomeDefinition("one", 262, 10000, [new ProgressGrant(dropMoonsUnique, 1)]),
                            new ActivityOutcomeDefinition("two", 2, 10000, [new ProgressGrant(dropMoonsUnique, 2)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // General Graardor: chestplate/tassets/boots 1/381 each, hilt 1/508. ~118s per kill, solo. Requires God Wars.
            new ActivitySeedDef(
                "boss.general_graardor", "General Graardor",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(118, TimeDistribution.Uniform, 25),
                        [
                            new ActivityOutcomeDefinition("nothing", 1509, 1524, []),
                            new ActivityOutcomeDefinition("unique", 15, 1524, [new ProgressGrant(dropGraardorUnique, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Commander Zilyana: Sword 1/127, Light 1/254, Crossbow 1/508, Hilt 1/508 (combined 2/127), pet 1/5000. ~126s per kill, solo. Requires God Wars.
            new ActivitySeedDef(
                "boss.commander_zilyana", "Commander Zilyana",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(126, TimeDistribution.Uniform, 25),
                        [
                            new ActivityOutcomeDefinition("nothing", 61905, 63000, []),
                            new ActivityOutcomeDefinition("unique", 1000, 63000, [new ProgressGrant(dropZilyanaUnique, 1)]),
                            new ActivityOutcomeDefinition("pet", 95, 63000, [new ProgressGrant(dropZilyanaUnique, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Vampyre Kraken: 1/512 Bottled Storm, 1 storm = 50 progress for storm tile. ~176s per kill, solo. Requires 78 Sailing.
            new ActivitySeedDef(
                "boss.vampyre_kraken", "Vampyre Kraken",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(176, TimeDistribution.Uniform, 35),
                        [
                            new ActivityOutcomeDefinition("nothing", 511, 512, []),
                            new ActivityOutcomeDefinition("storm", 1, 512, [new ProgressGrant(dropBottledStorm, 50)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Albatross: 1/30 Swift albatross feather per kill, 1 feather = 1 progress. ~60s per kill, solo.
            new ActivitySeedDef(
                "monster.albatross", "Albatross",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(60, TimeDistribution.Uniform, 12),
                        [
                            new ActivityOutcomeDefinition("nothing", 29, 30, []),
                            new ActivityOutcomeDefinition("feather", 1, 30, [new ProgressGrant(dropSwiftAlbatrossFeather, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Monkey agility: 1000 laps, 36s per lap. Pet 1/37,720 completes tile instantly (1000 progress).
            new ActivitySeedDef(
                "activity.monkey_agility", "Monkey Agility",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("lap", RollScope.PerPlayer, new AttemptTimeModel(36, TimeDistribution.Uniform, 6),
                        [
                            new ActivityOutcomeDefinition("lap", 37719, 37720, [new ProgressGrant(dropMonkeyLap, 1)]),
                            new ActivityOutcomeDefinition("pet", 1, 37720, [new ProgressGrant(dropMonkeyLap, 1000)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Sailing treasure: abstract 2 drops at 1/200 per attempt, ~135s per attempt.
            new ActivitySeedDef(
                "activity.sailing_treasure", "Sailing Treasure",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("attempt", RollScope.PerPlayer, new AttemptTimeModel(135, TimeDistribution.Uniform, 30),
                        [
                            new ActivityOutcomeDefinition("nothing", 199, 200, []),
                            new ActivityOutcomeDefinition("unique", 1, 200, [new ProgressGrant(dropSailingTreasureUnique, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Duke Sucellus: Magus 1/720, Eye 1/720, Virtus 3×1/2160 (combined 1/240), pet 1/2500 (counts as 1), ~77s per kill, solo. Requires DT2.
            new ActivitySeedDef(
                "boss.duke_sucellus", "Duke Sucellus",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(77, TimeDistribution.Uniform, 15),
                        [
                            new ActivityOutcomeDefinition("nothing", 218, 219, []),
                            new ActivityOutcomeDefinition("unique", 1, 219, [new ProgressGrant(dropDukeUnique, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Theatre of Blood: 3–5 players, 30 min per raid. 3 rolls per player at 1/15 for vials (45–60 per roll); 1/9 unique (100); pet 1/650 (100). PerGroup.
            new ActivitySeedDef(
                "raid.tob", "Theatre of Blood",
                new ActivityModeSupport(false, true, 3, 5),
                [
                    new ActivityAttemptDefinition("completion", RollScope.PerGroup, new AttemptTimeModel(1800, TimeDistribution.Uniform, 300),
                        [
                            new ActivityOutcomeDefinition("unique_no_pet", 649, 5850, [new ProgressGrant(dropTobVials, 100)]),
                            new ActivityOutcomeDefinition("unique_pet", 1, 5850, [new ProgressGrant(dropTobVials, 200)]),
                            new ActivityOutcomeDefinition("zero", 2288, 5850, []),
                            new ActivityOutcomeDefinition("one", 1969, 5850, [new ProgressGrant(dropTobVials, 45, 60)]),
                            new ActivityOutcomeDefinition("two", 725, 5850, [new ProgressGrant(dropTobVials, 90, 120)]),
                            new ActivityOutcomeDefinition("three", 208, 5850, [new ProgressGrant(dropTobVials, 135, 180)]),
                            new ActivityOutcomeDefinition("zero_pet", 4, 5850, [new ProgressGrant(dropTobVials, 100)]),
                            new ActivityOutcomeDefinition("one_pet", 3, 5850, [new ProgressGrant(dropTobVials, 145, 160)]),
                            new ActivityOutcomeDefinition("two_pet", 1, 5850, [new ProgressGrant(dropTobVials, 190, 220)]),
                            new ActivityOutcomeDefinition("three_pet", 1, 5850, [new ProgressGrant(dropTobVials, 235, 280)]),
                        ]),
                ],
                [new GroupSizeBand(3, 5, 1.0m, 1.0m)]),

            // Port task: Shark paint 1/36 per attempt, ~1200s (20 min) per attempt, solo.
            new ActivitySeedDef(
                "activity.port_task", "Port Task",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("task", RollScope.PerPlayer, new AttemptTimeModel(1200, TimeDistribution.Uniform, 300),
                        [
                            new ActivityOutcomeDefinition("nothing", 35, 36, []),
                            new ActivityOutcomeDefinition("paint", 1, 36, [new ProgressGrant(dropSharkPaint, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Zulrah: 2 drops per kill, each 3/1024 for unique (Tanzanite/Magic/Serpentine). Pet 1/4000 counts as 1. ~72s per kill, solo.
            new ActivitySeedDef(
                "boss.zulrah", "Zulrah",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(72, TimeDistribution.Uniform, 15),
                        [
                            new ActivityOutcomeDefinition("zero", 99385, 100000, []),
                            new ActivityOutcomeDefinition("one", 610, 100000, [new ProgressGrant(dropZulrahUnique, 1)]),
                            new ActivityOutcomeDefinition("two", 4, 100000, [new ProgressGrant(dropZulrahUnique, 2)]),
                            new ActivityOutcomeDefinition("two_plus_pet", 1, 100000, [new ProgressGrant(dropZulrahUnique, 3)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Great white shark: Broken dragon hook 1/1023, grants 75 progress (completes tile). ~90s per kill, solo. Requires sailing 75.
            new ActivitySeedDef(
                "boss.great_white_shark", "Great White Shark",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(90, TimeDistribution.Uniform, 18),
                        [
                            new ActivityOutcomeDefinition("nothing", 1022, 1023, []),
                            new ActivityOutcomeDefinition("hook", 1, 1023, [new ProgressGrant(dropHookOrBarbs, 75)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Ray barbs: 1/22 per kill from sailing monsters, 1 barb = 1 progress. ~57s per kill, solo. No requirement.
            new ActivitySeedDef(
                "monster.ray_barbs", "Ray Barbs",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(57, TimeDistribution.Uniform, 12),
                        [
                            new ActivityOutcomeDefinition("nothing", 21, 22, []),
                            new ActivityOutcomeDefinition("barb", 1, 22, [new ProgressGrant(dropHookOrBarbs, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Theatre of Blood Hard Mode: Holy 1/100, Sanguine ornament 1/150, Sanguine dust 1/275, pet 1/500. 30 min per raid, 3–5 players. PerGroup.
            new ActivitySeedDef(
                "raid.tob_hard_mode", "Theatre of Blood Hard Mode",
                new ActivityModeSupport(false, true, 3, 5),
                [
                    new ActivityAttemptDefinition("completion", RollScope.PerGroup, new AttemptTimeModel(1800, TimeDistribution.Uniform, 300),
                        [
                            new ActivityOutcomeDefinition("nothing", 16132, 16500, []),
                            new ActivityOutcomeDefinition("holy", 165, 16500, [new ProgressGrant(dropRaidCosmetic, 1)]),
                            new ActivityOutcomeDefinition("sanguine_ornament", 110, 16500, [new ProgressGrant(dropRaidCosmetic, 1)]),
                            new ActivityOutcomeDefinition("sanguine_dust", 60, 16500, [new ProgressGrant(dropRaidCosmetic, 1)]),
                            new ActivityOutcomeDefinition("pet", 33, 16500, [new ProgressGrant(dropRaidCosmetic, 1)]),
                        ]),
                ],
                [new GroupSizeBand(3, 5, 1.0m, 1.0m)]),

            // Tempoross: ~17 rolls per completion. 7 pages 1/54, barrel/tackle 1/400 each (25), tome 1/1600 (150), harpoon/pet 1/8000 each (300). ~600s (10 min) per completion.
            new ActivitySeedDef(
                "minigame.tempoross", "Tempoross",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("completion", RollScope.PerPlayer, new AttemptTimeModel(600, TimeDistribution.Uniform, 120),
                        [
                            new ActivityOutcomeDefinition("zero", 7100, 10000, []),
                            new ActivityOutcomeDefinition("seven", 2300, 10000, [new ProgressGrant(dropSoakedPages, 7)]),
                            new ActivityOutcomeDefinition("twenty_five", 500, 10000, [new ProgressGrant(dropSoakedPages, 25)]),
                            new ActivityOutcomeDefinition("thirty_two", 50, 10000, [new ProgressGrant(dropSoakedPages, 32)]),
                            new ActivityOutcomeDefinition("fifty", 30, 10000, [new ProgressGrant(dropSoakedPages, 50)]),
                            new ActivityOutcomeDefinition("seventy_five", 10, 10000, [new ProgressGrant(dropSoakedPages, 75)]),
                            new ActivityOutcomeDefinition("hundred_fifty", 5, 10000, [new ProgressGrant(dropSoakedPages, 150)]),
                            new ActivityOutcomeDefinition("complete", 5, 10000, [new ProgressGrant(dropSoakedPages, 300)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Phantom Muspah: Venator shard 1/100, pet 1/2500 (counts as 1). ~150s per kill, solo. Requires Secrets of the North.
            new ActivitySeedDef(
                "boss.phantom_muspah", "Phantom Muspah",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(150, TimeDistribution.Uniform, 30),
                        [
                            new ActivityOutcomeDefinition("nothing", 2474, 2500, []),
                            new ActivityOutcomeDefinition("shard", 25, 2500, [new ProgressGrant(dropVenatorShard, 1)]),
                            new ActivityOutcomeDefinition("pet", 1, 2500, [new ProgressGrant(dropVenatorShard, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Colosseum: 1/40 unique per completion. ~198s per completion, solo.
            new ActivitySeedDef(
                "activity.colosseum", "Colosseum",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("completion", RollScope.PerPlayer, new AttemptTimeModel(198, TimeDistribution.Uniform, 40),
                        [
                            new ActivityOutcomeDefinition("nothing", 39, 40, []),
                            new ActivityOutcomeDefinition("unique", 1, 40, [new ProgressGrant(dropColosseumUnique, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Nex: 2 rolls per kill, PerGroup. Torva 3/258, Zaryte 1/172, Nihil horn 1/258, Ancient hilt 1/516. Shards 1/16 (80-85), 1/26 (85-95). Row 10: torva=1500; Row 13: any unique=900.
            new ActivitySeedDef(
                "boss.nex", "Nex",
                new ActivityModeSupport(false, true, 3, 12),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerGroup, new AttemptTimeModel(3600, TimeDistribution.Uniform, 600),
                        [
                            new ActivityOutcomeDefinition("nothing", 7643, 10000, []),
                            new ActivityOutcomeDefinition("zaryte", 113, 10000, [new ProgressGrant(dropNexUniqueOrShardsR13, 900)]),
                            new ActivityOutcomeDefinition("nihil_horn", 76, 10000, [new ProgressGrant(dropNexUniqueOrShardsR13, 900)]),
                            new ActivityOutcomeDefinition("ancient_hilt", 38, 10000, [new ProgressGrant(dropNexUniqueOrShardsR13, 900)]),
                            new ActivityOutcomeDefinition("one_torva", 206, 10000, [new ProgressGrant(dropNexTorvaOrShards, 1500), new ProgressGrant(dropNexUniqueOrShardsR13, 900)]),
                            new ActivityOutcomeDefinition("two_torva", 1, 10000, [new ProgressGrant(dropNexTorvaOrShards, 1500), new ProgressGrant(dropNexUniqueOrShardsR13, 900)]),
                            new ActivityOutcomeDefinition("shards_80_85", 1110, 10000, [new ProgressGrant(dropNexTorvaOrShards, 82), new ProgressGrant(dropNexUniqueOrShardsR13, 82)]),
                            new ActivityOutcomeDefinition("shards_85_95", 680, 10000, [new ProgressGrant(dropNexTorvaOrShards, 90), new ProgressGrant(dropNexUniqueOrShardsR13, 90)]),
                            new ActivityOutcomeDefinition("both_shards", 48, 10000, [new ProgressGrant(dropNexTorvaOrShards, 172), new ProgressGrant(dropNexUniqueOrShardsR13, 172)]),
                            new ActivityOutcomeDefinition("torva_plus_shards", 85, 10000, [new ProgressGrant(dropNexTorvaOrShards, 1500), new ProgressGrant(dropNexUniqueOrShardsR13, 900)]),
                        ]),
                ],
                [new GroupSizeBand(3, 12, 1.0m, 1.0m)]),

            // Guardians of the Rift: Abyssal dye 1/400 per point, pet 1/4000 (counts as dye). ~128s per point, solo.
            new ActivitySeedDef(
                "minigame.guardians_of_the_rift", "Guardians of the Rift",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("point", RollScope.PerPlayer, new AttemptTimeModel(128, TimeDistribution.Uniform, 25),
                        [
                            new ActivityOutcomeDefinition("nothing", 3989, 4000, []),
                            new ActivityOutcomeDefinition("dye", 10, 4000, [new ProgressGrant(dropAbyssalDye, 1)]),
                            new ActivityOutcomeDefinition("pet", 1, 4000, [new ProgressGrant(dropAbyssalDye, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Sailing trials: Barracuda paint 1/220 per attempt. ~295s per attempt, solo.
            new ActivitySeedDef(
                "activity.sailing_trials", "Sailing Trials",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("attempt", RollScope.PerPlayer, new AttemptTimeModel(295, TimeDistribution.Uniform, 60),
                        [
                            new ActivityOutcomeDefinition("nothing", 219, 220, []),
                            new ActivityOutcomeDefinition("paint", 1, 220, [new ProgressGrant(dropBarracudaPaint, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // The Whisperer: Bellator 1/512, Siren's 1/512, Virtus 3×1/1536 (combined 3/512), pet 1/2000. ~75s per kill, solo. Requires DT2.
            new ActivitySeedDef(
                "boss.the_whisperer", "The Whisperer",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(75, TimeDistribution.Uniform, 15),
                        [
                            new ActivityOutcomeDefinition("nothing", 156, 157, []),
                            new ActivityOutcomeDefinition("unique", 1, 157, [new ProgressGrant(dropWhispererUnique, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Brine rat: Brine sabre 1/512. ~49s per kill, solo.
            new ActivitySeedDef(
                "monster.brine_rat", "Brine Rat",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(49, TimeDistribution.Uniform, 10),
                        [
                            new ActivityOutcomeDefinition("nothing", 511, 512, []),
                            new ActivityOutcomeDefinition("sabre", 1, 512, [new ProgressGrant(dropBrineSabre, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Grotesque Guardians: 2 loot rolls per kill (gloves 1/500, ring 1/500, hammer 1/750, core 1/1000 each), pet 1/3000 independent. ~78s per kill, solo.
            new ActivitySeedDef(
                "boss.grotesque_guardians", "Grotesque Guardians",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(78, TimeDistribution.Uniform, 15),
                        [
                            new ActivityOutcomeDefinition("zero", 9860, 10000, []),
                            new ActivityOutcomeDefinition("one", 125, 10000, [new ProgressGrant(dropGrotesqueUnique, 1)]),
                            new ActivityOutcomeDefinition("two", 14, 10000, [new ProgressGrant(dropGrotesqueUnique, 2)]),
                            new ActivityOutcomeDefinition("three", 1, 10000, [new ProgressGrant(dropGrotesqueUnique, 3)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Tombs of Amascut: 1/5 per raid one player gets unique (50 battlestaff). Others: 3 rolls at 1/27 for battlestaff (15-25 each). ~30 min per raid, 1-8 players. PerGroup.
            new ActivitySeedDef(
                "raid.toa", "Tombs of Amascut",
                new ActivityModeSupport(true, true, 1, 8),
                [
                    new ActivityAttemptDefinition("completion", RollScope.PerGroup, new AttemptTimeModel(1800, TimeDistribution.Uniform, 300),
                        [
                            new ActivityOutcomeDefinition("unique", 2000, 10000, [new ProgressGrant(dropToaBattlestaff, 50)]),
                            new ActivityOutcomeDefinition("zero", 5120, 10000, []),
                            new ActivityOutcomeDefinition("one_staff", 2320, 10000, [new ProgressGrant(dropToaBattlestaff, 15, 25)]),
                            new ActivityOutcomeDefinition("two_staff", 480, 10000, [new ProgressGrant(dropToaBattlestaff, 30, 50)]),
                            new ActivityOutcomeDefinition("three_staff", 80, 10000, [new ProgressGrant(dropToaBattlestaff, 45, 75)]),
                        ]),
                ],
                [new GroupSizeBand(1, 8, 1.0m, 1.0m)]),

            // The Leviathan: Venator 1/768, Lure 1/768, Virtus 3×1/2304 (combined 3/768), pet 1/2500. ~58s per kill, solo. Requires DT2.
            new ActivitySeedDef(
                "boss.the_leviathan", "The Leviathan",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(58, TimeDistribution.Uniform, 12),
                        [
                            new ActivityOutcomeDefinition("nothing", 233, 234, []),
                            new ActivityOutcomeDefinition("unique", 1, 234, [new ProgressGrant(dropLeviathanUnique, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Pyramid Plunder: Pharaoh sceptre 1/138 per attempt. ~130s per attempt, solo.
            new ActivitySeedDef(
                "minigame.pyramid_plunder", "Pyramid Plunder",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("attempt", RollScope.PerPlayer, new AttemptTimeModel(130, TimeDistribution.Uniform, 30),
                        [
                            new ActivityOutcomeDefinition("nothing", 137, 138, []),
                            new ActivityOutcomeDefinition("sceptre", 1, 138, [new ProgressGrant(dropPharaohSceptre, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Abyssal Sire: 1/100 unique per kill. ~180s per kill, solo. Requires 85 Slayer.
            new ActivitySeedDef(
                "boss.abyssal_sire", "Abyssal Sire",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(180, TimeDistribution.Uniform, 35),
                        [
                            new ActivityOutcomeDefinition("nothing", 99, 100, []),
                            new ActivityOutcomeDefinition("unique", 1, 100, [new ProgressGrant(dropAbyssalSireUnique, 1)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Scurrius: 1/33 spine, 1/3000 pet (counts as 3 spines). ~66s per kill, solo. Derived: 12 spines in 7h.
            new ActivitySeedDef(
                "boss.scurrius", "Scurrius",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("kill", RollScope.PerPlayer, new AttemptTimeModel(66, TimeDistribution.Uniform, 12),
                        [
                            new ActivityOutcomeDefinition("nothing", 31989, 33000, []),
                            new ActivityOutcomeDefinition("spine", 1000, 33000, [new ProgressGrant(dropScurriusSpine, 1)]),
                            new ActivityOutcomeDefinition("pet", 11, 33000, [new ProgressGrant(dropScurriusSpine, 3)]),
                        ]),
                ],
                [new GroupSizeBand(1, 1, 1.0m, 1.0m)]),

            // Sailing Dragon Metal: abstract activity for Dragon Metal Sheets from various Sailing activities. 1/100 per attempt, ~60s per attempt. 9 sheets in ~15h.
            new ActivitySeedDef(
                "activity.sailing_dragon_metal", "Sailing Dragon Metal",
                new ActivityModeSupport(true, false, null, null),
                [
                    new ActivityAttemptDefinition("attempt", RollScope.PerPlayer, new AttemptTimeModel(60, TimeDistribution.Uniform, 15),
                        [
                            new ActivityOutcomeDefinition("nothing", 99, 100, []),
                            new ActivityOutcomeDefinition("sheet", 1, 100, [new ProgressGrant(dropDragonMetalSheet, 1)]),
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
        var slayerId = activityIdsByKey["activity.slayer"];
        var slayerKey = "activity.slayer";
        var barrowsId = activityIdsByKey["boss.barrows"];
        var barrowsKey = "boss.barrows";
        var doomId = activityIdsByKey["boss.doom_of_mokhaiotl"];
        var doomKey = "boss.doom_of_mokhaiotl";
        var araxxorId = activityIdsByKey["boss.araxxor"];
        var araxxorKey = "boss.araxxor";
        var moonsOfPerilId = activityIdsByKey["boss.moons_of_peril"];
        var moonsOfPerilKey = "boss.moons_of_peril";
        var graardorId = activityIdsByKey["boss.general_graardor"];
        var graardorKey = "boss.general_graardor";
        var vampyreKrakenId = activityIdsByKey["boss.vampyre_kraken"];
        var vampyreKrakenKey = "boss.vampyre_kraken";
        var albatrossId = activityIdsByKey["monster.albatross"];
        var albatrossKey = "monster.albatross";
        var monkeyAgilityId = activityIdsByKey["activity.monkey_agility"];
        var monkeyAgilityKey = "activity.monkey_agility";
        var sailingTreasureId = activityIdsByKey["activity.sailing_treasure"];
        var sailingTreasureKey = "activity.sailing_treasure";
        var dukeSucellusId = activityIdsByKey["boss.duke_sucellus"];
        var dukeSucellusKey = "boss.duke_sucellus";
        var tobId = activityIdsByKey["raid.tob"];
        var tobKey = "raid.tob";
        var portTaskId = activityIdsByKey["activity.port_task"];
        var portTaskKey = "activity.port_task";
        var zulrahId = activityIdsByKey["boss.zulrah"];
        var zulrahKey = "boss.zulrah";
        var greatWhiteSharkId = activityIdsByKey["boss.great_white_shark"];
        var greatWhiteSharkKey = "boss.great_white_shark";
        var rayBarbsId = activityIdsByKey["monster.ray_barbs"];
        var rayBarbsKey = "monster.ray_barbs";
        var tobHmId = activityIdsByKey["raid.tob_hard_mode"];
        var tobHmKey = "raid.tob_hard_mode";
        var zilyanaId = activityIdsByKey["boss.commander_zilyana"];
        var zilyanaKey = "boss.commander_zilyana";
        var temporossId = activityIdsByKey["minigame.tempoross"];
        var temporossKey = "minigame.tempoross";
        var phantomMuspahId = activityIdsByKey["boss.phantom_muspah"];
        var phantomMuspahKey = "boss.phantom_muspah";
        var colosseumId = activityIdsByKey["activity.colosseum"];
        var colosseumKey = "activity.colosseum";
        var nexId = activityIdsByKey["boss.nex"];
        var nexKey = "boss.nex";
        var gotrId = activityIdsByKey["minigame.guardians_of_the_rift"];
        var gotrKey = "minigame.guardians_of_the_rift";
        var sailingTrialsId = activityIdsByKey["activity.sailing_trials"];
        var sailingTrialsKey = "activity.sailing_trials";
        var whispererId = activityIdsByKey["boss.the_whisperer"];
        var whispererKey = "boss.the_whisperer";
        var brineRatId = activityIdsByKey["monster.brine_rat"];
        var brineRatKey = "monster.brine_rat";
        var grotesqueGuardiansId = activityIdsByKey["boss.grotesque_guardians"];
        var grotesqueGuardiansKey = "boss.grotesque_guardians";
        var toaId = activityIdsByKey["raid.toa"];
        var toaKey = "raid.toa";
        var leviathanId = activityIdsByKey["boss.the_leviathan"];
        var leviathanKey = "boss.the_leviathan";
        var pyramidPlunderId = activityIdsByKey["minigame.pyramid_plunder"];
        var pyramidPlunderKey = "minigame.pyramid_plunder";
        var abyssalSireId = activityIdsByKey["boss.abyssal_sire"];
        var abyssalSireKey = "boss.abyssal_sire";
        var scurriusId = activityIdsByKey["boss.scurrius"];
        var scurriusKey = "boss.scurrius";
        var sailingDragonMetalId = activityIdsByKey["activity.sailing_dragon_metal"];
        var sailingDragonMetalKey = "activity.sailing_dragon_metal";

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
        var questFinalDawn = new Capability("quest.the_final_dawn", "The Final Dawn");
        var questPerilousMoons = new Capability("quest.perilous_moons", "Perilous Moons");
        var capabilityGodWars = new Capability("capability.god_wars", "God Wars");
        var sailing78 = new Capability("sailing.78", "Sailing 78");
        var questDt2 = new Capability("quest.dt2", "Desert Treasure II");
        var raidTob = new Capability("raid.tob", "Theatre of Blood");
        var sailing75 = new Capability("sailing.75", "Sailing 75");
        var raidTobHm = new Capability("raid.tob_hard_mode", "Hard Mode Theatre of Blood");
        var questSecretsOfNorth = new Capability("quest.secrets_of_the_north", "Secrets of the North");
        var raidToa = new Capability("raid.toa", "Tombs of Amascut");
        var slayer85 = new Capability("slayer.85", "Slayer 85");

        var row0 = new Row(0,
        [
            new Tile("t1-r0", "3x Merfolk Trident", 1, 3,
                [new TileActivityRule(underwaterId, underwaterKey, ["item.merfolk_trident"], [], [])]),
            new Tile("t2-r0", "2x Belle's Folly", 2, 2,
                [new TileActivityRule(gryphonId, gryphonKey, ["item.belles_folly"], [slayer51], [])]),
            new Tile("t3-r0", "1x Dragon Warhammer", 3, 1,
                [new TileActivityRule(shamanId, shamanKey, ["item.dragon_warhammer"], [], [])]),
            new Tile("t4-r0", "1100 Dragon Arrows", 4, 1100,
                [new TileActivityRule(coxId, coxKey, ["loot.dragon_arrows"], [raidCox], [])]),
        ]);

        var row1 = new Row(1,
        [
            new Tile("t1-r1", "6x DK Ring", 1, 6,
                [new TileActivityRule(dkId, dkKey, ["item.dk_ring"], [], [])]),
            new Tile("t2-r1", "2x TD Unique", 2, 2,
                [new TileActivityRule(tdId, tdKey, ["item.td_unique"], [questWgs], [])]),
            new Tile("t3-r1", "5x Cerberus Unique", 3, 5,
                [new TileActivityRule(cerberusId, cerberusKey, ["item.cerberus_unique"], [slayer91], [])]),
            new Tile("t4-r1", "12x Vorkath Item", 4, 12,
                [new TileActivityRule(vorkathId, vorkathKey, ["item.vorkath_item"], [questDs2], [])]),
        ]);

        var row2 = new Row(2,
        [
            new Tile("t1-r2", "3x Elder Chaos Druid Robes", 1, 3,
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
            new Tile("t4-r3", "2x Callisto Hilt", 4, 2,
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

        var row5 = new Row(5,
        [
            new Tile("t1-r5", "18x Slayer Items", 1, 18,
                [new TileActivityRule(slayerId, slayerKey, ["item.slayer_item"], [], [])]),
            new Tile("t2-r5", "8x Barrows Unique", 2, 8,
                [new TileActivityRule(barrowsId, barrowsKey, ["item.barrows_unique"], [], [])]),
            new Tile("t3-r5", "2x Doom Unique", 3, 2,
                [new TileActivityRule(doomId, doomKey, ["item.doom_unique"], [questFinalDawn], [])]),
            new Tile("t4-r5", "4x Araxxor Unique", 4, 4,
                [new TileActivityRule(araxxorId, araxxorKey, ["item.araxxor_unique"], [], [])]),
        ]);

        var row6 = new Row(6,
        [
            new Tile("t1-r6", "7x Moons of Peril Unique", 1, 7,
                [new TileActivityRule(moonsOfPerilId, moonsOfPerilKey, ["item.moons_unique"], [questPerilousMoons], [])]),
            new Tile("t2-r6", "3x General Graardor Unique", 2, 3,
                [new TileActivityRule(graardorId, graardorKey, ["item.graardor_unique"], [capabilityGodWars], [])]),
            new Tile("t3-r6", "810 Dragon Arrows", 3, 810,
                [new TileActivityRule(coxId, coxKey, ["loot.dragon_arrows"], [raidCox], [])]),
            new Tile("t4-r6", "Storm Path (1 Bottled Storm or 50 Feathers)", 4, 50,
                [
                    new TileActivityRule(vampyreKrakenId, vampyreKrakenKey, ["item.bottled_storm"], [sailing78], []),
                    new TileActivityRule(albatrossId, albatrossKey, ["item.swift_albatross_feather"], [], []),
                ]),
        ]);

        var row7 = new Row(7,
        [
            new Tile("t1-r7", "1000 Monkey Agility Laps", 1, 1000,
                [new TileActivityRule(monkeyAgilityId, monkeyAgilityKey, ["progress.monkey_lap"], [], [])]),
            new Tile("t2-r7", "2x Sailing Treasure Unique", 2, 2,
                [new TileActivityRule(sailingTreasureId, sailingTreasureKey, ["item.sailing_treasure_unique"], [], [])]),
            new Tile("t3-r7", "4x Duke Sucellus Unique", 3, 4,
                [new TileActivityRule(dukeSucellusId, dukeSucellusKey, ["item.duke_unique"], [questDt2], [])]),
            new Tile("t4-r7", "850 ToB Vials", 4, 850,
                [new TileActivityRule(tobId, tobKey, ["loot.tob_vials"], [raidTob], [])]),
        ]);

        var row8 = new Row(8,
        [
            new Tile("t1-r8", "5x Shark Paint", 1, 5,
                [new TileActivityRule(portTaskId, portTaskKey, ["item.shark_paint"], [], [])]),
            new Tile("t2-r8", "4x Zulrah Unique", 2, 4,
                [new TileActivityRule(zulrahId, zulrahKey, ["item.zulrah_unique"], [], [])]),
            new Tile("t3-r8", "Dragon Hook or 75 Ray Barbs", 3, 75,
                [
                    new TileActivityRule(greatWhiteSharkId, greatWhiteSharkKey, ["item.hook_or_barbs"], [sailing75], []),
                    new TileActivityRule(rayBarbsId, rayBarbsKey, ["item.hook_or_barbs"], [], []),
                ]),
            new Tile("t4-r8", "1 Raid Cosmetic (CoX or ToB HM)", 4, 1,
                [
                    new TileActivityRule(coxId, coxKey, ["loot.raid_cosmetic"], [raidCox], []),
                    new TileActivityRule(tobHmId, tobHmKey, ["loot.raid_cosmetic"], [raidTobHm], []),
                ]),
        ]);

        var row9 = new Row(9,
        [
            new Tile("t1-r9", "6x Fedora", 1, 6,
                [new TileActivityRule(crazyArchId, crazyArchKey, ["item.fedora"], [], [])]),
            new Tile("t2-r9", "5x Commander Zilyana Unique", 2, 5,
                [new TileActivityRule(zilyanaId, zilyanaKey, ["item.zilyana_unique"], [capabilityGodWars], [])]),
            new Tile("t3-r9", "1 Nightmare Unique or 30 Bass", 3, 30,
                [
                    new TileActivityRule(nightmareId, nightmareKey, ["item.nightmare_r9"], [], []),
                    new TileActivityRule(phosaniId, phosaniKey, ["item.nightmare_r9"], [], []),
                ]),
            new Tile("t4-r9", "2x Yama Unique", 4, 2,
                [new TileActivityRule(yamaId, yamaKey, ["item.yama_unique"], [questKingdomDivided], [])]),
        ]);

        var row10 = new Row(10,
        [
            new Tile("t1-r10", "300 Soaked Pages", 1, 300,
                [new TileActivityRule(temporossId, temporossKey, ["item.soaked_pages"], [], [])]),
            new Tile("t2-r10", "3x Venator Shard", 2, 3,
                [new TileActivityRule(phantomMuspahId, phantomMuspahKey, ["item.venator_shard"], [questSecretsOfNorth], [])]),
            new Tile("t3-r10", "5x Colosseum Unique", 3, 5,
                [new TileActivityRule(colosseumId, colosseumKey, ["item.colosseum_unique"], [], [])]),
            new Tile("t4-r10", "1 Torva Piece or 1500 Nihil Shards", 4, 1500,
                [new TileActivityRule(nexId, nexKey, ["item.nex_torva_or_shards"], [capabilityGodWars], [])]),
        ]);

        var row11 = new Row(11,
        [
            new Tile("t1-r11", "1x Abyssal Dye", 1, 1,
                [new TileActivityRule(gotrId, gotrKey, ["item.abyssal_dye"], [], [])]),
            new Tile("t2-r11", "1x Barracuda Paint", 2, 1,
                [new TileActivityRule(sailingTrialsId, sailingTrialsKey, ["item.barracuda_paint"], [], [])]),
            new Tile("t3-r11", "4x The Whisperer Unique", 3, 4,
                [new TileActivityRule(whispererId, whispererKey, ["item.whisperer_unique"], [questDt2], [])]),
            new Tile("t4-r11", "2 Rev Weapon Points", 4, 2,
                [new TileActivityRule(revKnightId, revKnightKey, ["item.rev_weapon_points"], [], [])]),
        ]);

        var row12 = new Row(12,
        [
            new Tile("t1-r12", "1x Brine Sabre", 1, 1,
                [new TileActivityRule(brineRatId, brineRatKey, ["item.brine_sabre"], [], [])]),
            new Tile("t2-r12", "6x Grotesque Guardians Unique", 2, 6,
                [new TileActivityRule(grotesqueGuardiansId, grotesqueGuardiansKey, ["item.grotesque_unique"], [], [])]),
            new Tile("t3-r12", "110 ToA Battlestaff", 3, 110,
                [new TileActivityRule(toaId, toaKey, ["loot.toa_battlestaff"], [raidToa], [])]),
            new Tile("t4-r12", "4x The Leviathan Unique", 4, 4,
                [new TileActivityRule(leviathanId, leviathanKey, ["item.leviathan_unique"], [questDt2], [])]),
        ]);

        var row13 = new Row(13,
        [
            new Tile("t1-r13", "2x Pharaoh Sceptre", 1, 2,
                [new TileActivityRule(pyramidPlunderId, pyramidPlunderKey, ["item.pharaoh_sceptre"], [], [])]),
            new Tile("t2-r13", "2x Abyssal Sire Unique", 2, 2,
                [new TileActivityRule(abyssalSireId, abyssalSireKey, ["item.abyssal_sire_unique"], [slayer85], [])]),
            new Tile("t3-r13", "1 Nex Unique or 900 Nihil Shards", 3, 900,
                [new TileActivityRule(nexId, nexKey, ["item.nex_unique_or_shards_r13"], [capabilityGodWars], [])]),
            new Tile("t4-r13", "850 ToB Vials", 4, 850,
                [new TileActivityRule(tobId, tobKey, ["loot.tob_vials"], [raidTob], [])]),
        ]);

        var row14 = new Row(14,
        [
            new Tile("t1-r14", "12x Scurrius Spine", 1, 12,
                [new TileActivityRule(scurriusId, scurriusKey, ["item.scurrius_spine"], [], [])]),
            new Tile("t2-r14", "9x Dragon Metal Sheets", 2, 9,
                [new TileActivityRule(sailingDragonMetalId, sailingDragonMetalKey, ["loot.dragon_metal_sheet"], [], [])]),
            new Tile("t3-r14", "525 ToB Vials", 3, 525,
                [new TileActivityRule(tobId, tobKey, ["loot.tob_vials"], [raidTob], [])]),
            new Tile("t4-r14", "150 ToA Battlestaff", 4, 150,
                [new TileActivityRule(toaId, toaKey, ["loot.toa_battlestaff"], [raidToa], [])]),
        ]);

        var existing = await _eventRepo.GetByNameAsync(eventName, cancellationToken);

        if (existing is not null)
        {
            existing.UpdateDuration(duration);
            existing.SetUnlockPointsRequiredPerRow(unlockPointsPerRow);
            existing.SetRows([row0, row1, row2, row3, row4, row5, row6, row7, row8, row9, row10, row11, row12, row13, row14]);
            await _eventRepo.UpdateAsync(existing, cancellationToken);
            logger.LogInformation("Real event seed: updated event '{Name}'", eventName);
        }
        else
        {
            var evt = new Event(eventName, duration, unlockPointsPerRow);
            evt.SetRows([row0, row1, row2, row3, row4, row5, row6, row7, row8, row9, row10, row11, row12, row13, row14]);
            await _eventRepo.AddAsync(evt, cancellationToken);
            logger.LogInformation("Real event seed: created event '{Name}'", eventName);
        }
    }

    private sealed record ActivitySeedDef(string Key, string Name, ActivityModeSupport ModeSupport, List<ActivityAttemptDefinition> Attempts, List<GroupSizeBand> GroupScalingBands);
}
