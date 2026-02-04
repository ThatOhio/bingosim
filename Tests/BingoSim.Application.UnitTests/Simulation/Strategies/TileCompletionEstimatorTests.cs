using BingoSim.Application.Simulation.Snapshot;
using BingoSim.Application.Simulation.Strategies;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation.Strategies;

public class TileCompletionEstimatorTests
{
    [Fact]
    public void EstimateCompletionTime_RequiredCountZero_ReturnsZero()
    {
        var tile = CreateTile("t1", 0, CreateRule(Guid.NewGuid(), "drop"));
        var snapshot = CreateSnapshot([], []);
        TileCompletionEstimator.EstimateCompletionTime(tile, snapshot).Should().Be(0.0);
    }

    [Fact]
    public void EstimateCompletionTime_ActivityNotFound_ReturnsMaxValue()
    {
        var actId = Guid.NewGuid();
        var tile = CreateTile("t1", 1, CreateRule(actId, "drop"));
        var snapshot = CreateSnapshot([], []); // No activities
        TileCompletionEstimator.EstimateCompletionTime(tile, snapshot).Should().Be(double.MaxValue);
    }

    [Fact]
    public void EstimateCompletionTime_100PercentDropOneUnit_ReturnsAttemptTime()
    {
        var actId = Guid.NewGuid();
        var rule = CreateRule(actId, "drop");
        var tile = CreateTile("t1", 1, rule);
        var activity = CreateActivity(actId, 60, [CreateOutcome(1, 1, "drop", 1)]);
        var snapshot = CreateSnapshot([activity], []);

        var result = TileCompletionEstimator.EstimateCompletionTime(tile, snapshot);
        result.Should().Be(60.0);
    }

    [Fact]
    public void EstimateCompletionTime_RequiredCountTwo_ReturnsDoubleAttemptTime()
    {
        var actId = Guid.NewGuid();
        var rule = CreateRule(actId, "drop");
        var tile = CreateTile("t1", 2, rule);
        var activity = CreateActivity(actId, 60, [CreateOutcome(1, 1, "drop", 1)]);
        var snapshot = CreateSnapshot([activity], []);

        var result = TileCompletionEstimator.EstimateCompletionTime(tile, snapshot);
        result.Should().Be(120.0);
    }

    [Fact]
    public void EstimateCompletionTime_50PercentDrop_ReturnsDoubleTime()
    {
        var actId = Guid.NewGuid();
        var rule = CreateRule(actId, "drop");
        var tile = CreateTile("t1", 1, rule);
        var activity = CreateActivity(actId, 60, [
            CreateOutcome(1, 2, "drop", 1),
            CreateOutcome(1, 2, "other", 0)
        ]);
        var snapshot = CreateSnapshot([activity], []);

        var result = TileCompletionEstimator.EstimateCompletionTime(tile, snapshot);
        result.Should().BeApproximately(120.0, 0.01);
    }

    [Fact]
    public void EstimateCompletionTime_NoMatchingGrants_ReturnsMaxValue()
    {
        var actId = Guid.NewGuid();
        var rule = CreateRule(actId, "drop");
        var tile = CreateTile("t1", 1, rule);
        var activity = CreateActivity(actId, 60, [CreateOutcome(1, 1, "other", 1)]);
        var snapshot = CreateSnapshot([activity], []);

        TileCompletionEstimator.EstimateCompletionTime(tile, snapshot).Should().Be(double.MaxValue);
    }

    [Fact]
    public void EstimateCompletionTime_TwoActivities_ChoosesFastest()
    {
        var act1Id = Guid.NewGuid();
        var act2Id = Guid.NewGuid();
        var rule1 = CreateRule(act1Id, "drop1");
        var rule2 = CreateRule(act2Id, "drop2");
        var tile = CreateTile("t1", 1, rule1, rule2);

        var activity1 = CreateActivity(act1Id, 120, [CreateOutcome(1, 1, "drop1", 1)]);
        var activity2 = CreateActivity(act2Id, 60, [CreateOutcome(1, 1, "drop2", 1)]);
        var snapshot = CreateSnapshot([activity1, activity2], []);

        var result = TileCompletionEstimator.EstimateCompletionTime(tile, snapshot);
        result.Should().Be(60.0);
    }

    private static TileSnapshotDto CreateTile(string key, int requiredCount, params TileActivityRuleSnapshotDto[] rules)
    {
        return new TileSnapshotDto
        {
            Key = key,
            Name = key,
            Points = 1,
            RequiredCount = requiredCount,
            AllowedActivities = rules.ToList()
        };
    }

    private static TileActivityRuleSnapshotDto CreateRule(Guid activityId, string acceptedDropKey)
    {
        return new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = activityId,
            ActivityKey = "act",
            AcceptedDropKeys = [acceptedDropKey],
            RequirementKeys = [],
            Modifiers = []
        };
    }

    private static OutcomeSnapshotDto CreateOutcome(int num, int denom, string dropKey, int units)
    {
        return new OutcomeSnapshotDto
        {
            WeightNumerator = num,
            WeightDenominator = denom,
            Grants = units > 0 ? [new ProgressGrantSnapshotDto { DropKey = dropKey, Units = units }] : []
        };
    }

    private static ActivitySnapshotDto CreateActivity(Guid id, int baselineSeconds, OutcomeSnapshotDto[] outcomes)
    {
        return new ActivitySnapshotDto
        {
            Id = id,
            Key = "act",
            Attempts =
            [
                new AttemptSnapshotDto
                {
                    Key = "main",
                    RollScope = 0,
                    BaselineTimeSeconds = baselineSeconds,
                    VarianceSeconds = 0,
                    Outcomes = outcomes.ToList()
                }
            ],
            GroupScalingBands = [],
            ModeSupport = new ActivityModeSupportSnapshotDto { SupportsSolo = true, SupportsGroup = false }
        };
    }

    private static EventSnapshotDto CreateSnapshot(ActivitySnapshotDto[] activities, RowSnapshotDto[] rows)
    {
        var activitiesById = activities.ToDictionary(a => a.Id, a => a);
        return new EventSnapshotDto
        {
            EventName = "Test",
            DurationSeconds = 3600,
            UnlockPointsRequiredPerRow = 5,
            Rows = rows.ToList(),
            ActivitiesById = activitiesById,
            Teams = [],
            EventStartTimeEt = "2025-02-04T09:00:00-05:00"
        };
    }
}
