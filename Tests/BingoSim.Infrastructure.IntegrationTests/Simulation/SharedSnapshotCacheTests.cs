using BingoSim.Application.Simulation.Schedule;
using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Infrastructure.Simulation;
using FluentAssertions;

namespace BingoSim.Infrastructure.IntegrationTests.Simulation;

public class SharedSnapshotCacheTests
{
    [Fact]
    public void Get_EmptyCache_ReturnsNull()
    {
        var cache = new SharedSnapshotCache();
        var batchId = Guid.NewGuid();

        var result = cache.Get(batchId);

        result.Should().BeNull();
    }

    [Fact]
    public void Get_AfterSet_ReturnsCachedSnapshot()
    {
        var cache = new SharedSnapshotCache();
        var batchId = Guid.NewGuid();
        var snapshot = CreateMinimalSnapshot();

        cache.Set(batchId, snapshot);
        var result = cache.Get(batchId);

        result.Should().NotBeNull();
        result.Should().BeSameAs(snapshot);
    }

    [Fact]
    public void Get_DifferentBatch_ReturnsNull()
    {
        var cache = new SharedSnapshotCache();
        var batchId1 = Guid.NewGuid();
        var batchId2 = Guid.NewGuid();
        cache.Set(batchId1, CreateMinimalSnapshot());

        var result = cache.Get(batchId2);

        result.Should().BeNull();
    }

    [Fact]
    public void Get_AfterSet_ConcurrentAccess_ReturnsSameSnapshot()
    {
        var cache = new SharedSnapshotCache();
        var batchId = Guid.NewGuid();
        var snapshot = CreateMinimalSnapshot();
        cache.Set(batchId, snapshot);

        var results = Enumerable.Range(0, 100)
            .AsParallel()
            .Select(_ => cache.Get(batchId))
            .ToList();

        results.Should().AllBeEquivalentTo(snapshot);
    }

    [Fact]
    public void Set_OverCapacity_EvictsOldest()
    {
        var cache = new SharedSnapshotCache();
        var batchIds = Enumerable.Range(0, 40).Select(_ => Guid.NewGuid()).ToList();
        var snapshots = batchIds.Select(_ => CreateMinimalSnapshot()).ToList();

        for (var i = 0; i < batchIds.Count; i++)
            cache.Set(batchIds[i], snapshots[i]);

        // Cache is bounded to 32; first 8 should be evicted
        var evictedCount = 0;
        for (var i = 0; i < 8; i++)
        {
            if (cache.Get(batchIds[i]) is null)
                evictedCount++;
        }

        evictedCount.Should().BeGreaterThan(0, "eviction should have removed some entries");
        cache.Get(batchIds[^1]).Should().NotBeNull("most recently added should still be present");
    }

    private static EventSnapshotDto CreateMinimalSnapshot()
    {
        var actId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var eventStart = new DateTimeOffset(2025, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5));
        var alwaysOnline = new WeeklyScheduleSnapshotDto { Sessions = [] };

        return new EventSnapshotDto
        {
            EventName = "Test",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            EventStartTimeEt = eventStart.ToString("o"),
            Rows =
            [
                new RowSnapshotDto
                {
                    Index = 0,
                    Tiles =
                    [
                        new TileSnapshotDto
                        {
                            Key = "t1",
                            Name = "T1",
                            Points = 1,
                            RequiredCount = 1,
                            AllowedActivities =
                            [
                                new TileActivityRuleSnapshotDto
                                {
                                    ActivityDefinitionId = actId,
                                    ActivityKey = "act",
                                    AcceptedDropKeys = ["drop"],
                                    RequirementKeys = [],
                                    Modifiers = []
                                }
                            ]
                        }
                    ]
                }
            ],
            ActivitiesById = new Dictionary<Guid, ActivitySnapshotDto>
            {
                [actId] = new ActivitySnapshotDto
                {
                    Id = actId,
                    Key = "act",
                    Attempts =
                    [
                        new AttemptSnapshotDto
                        {
                            Key = "main",
                            RollScope = 0,
                            BaselineTimeSeconds = 60,
                            VarianceSeconds = 10,
                            Outcomes =
                            [
                                new OutcomeSnapshotDto
                                {
                                    WeightNumerator = 1,
                                    WeightDenominator = 1,
                                    Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }]
                                }
                            ]
                        }
                    ],
                    GroupScalingBands = [],
                    ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
                }
            },
            Teams =
            [
                new TeamSnapshotDto
                {
                    TeamId = teamId,
                    TeamName = "Team A",
                    StrategyKey = "RowUnlocking",
                    ParamsJson = null,
                    Players =
                    [
                        new PlayerSnapshotDto
                        {
                            PlayerId = playerId,
                            Name = "P1",
                            SkillTimeMultiplier = 1.0m,
                            CapabilityKeys = [],
                            Schedule = alwaysOnline
                        }
                    ]
                }
            ]
        };
    }
}
