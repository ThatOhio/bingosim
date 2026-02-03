using BingoSim.Application.Simulation;
using BingoSim.Application.Simulation.Snapshot;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

public class GroupScalingBandSelectorTests
{
    private static List<GroupSizeBandSnapshotDto> Bands(params (int min, int max, decimal time, decimal prob)[] bands) =>
        bands.Select(b => new GroupSizeBandSnapshotDto
        {
            MinSize = b.min,
            MaxSize = b.max,
            TimeMultiplier = b.time,
            ProbabilityMultiplier = b.prob
        }).ToList();

    [Fact]
    public void Select_GroupSizeInRange_ReturnsMatchingBand()
    {
        var bands = Bands((1, 1, 1.0m, 1.0m), (2, 4, 0.85m, 1.1m), (5, 8, 0.75m, 1.2m));

        var (time, prob) = GroupScalingBandSelector.Select(bands, 3);

        time.Should().Be(0.85m);
        prob.Should().Be(1.1m);
    }

    [Fact]
    public void Select_GroupSizeAtBoundary_ReturnsBand()
    {
        var bands = Bands((1, 1, 1.0m, 1.0m), (2, 4, 0.85m, 1.1m));

        var (time, prob) = GroupScalingBandSelector.Select(bands, 4);

        time.Should().Be(0.85m);
        prob.Should().Be(1.1m);
    }

    [Fact]
    public void Select_NoMatchingBand_ReturnsIdentityMultipliers()
    {
        var bands = Bands((1, 1, 1.0m, 1.0m), (2, 4, 0.85m, 1.1m), (5, 8, 0.75m, 1.2m));

        var (time, prob) = GroupScalingBandSelector.Select(bands, 10);

        time.Should().Be(1.0m);
        prob.Should().Be(1.0m);
    }

    [Fact]
    public void Select_EmptyBands_ReturnsIdentity()
    {
        var bands = new List<GroupSizeBandSnapshotDto>();

        var (time, prob) = GroupScalingBandSelector.Select(bands, 3);

        time.Should().Be(1.0m);
        prob.Should().Be(1.0m);
    }

    [Fact]
    public void Select_NullBands_ReturnsIdentity()
    {
        var (time, prob) = GroupScalingBandSelector.Select(null, 3);

        time.Should().Be(1.0m);
        prob.Should().Be(1.0m);
    }

    [Fact]
    public void Select_GroupSize1_ReturnsSoloBand()
    {
        var bands = Bands((1, 1, 1.0m, 1.0m), (2, 4, 0.85m, 1.1m));

        var (time, prob) = GroupScalingBandSelector.Select(bands, 1);

        time.Should().Be(1.0m);
        prob.Should().Be(1.0m);
    }

    [Fact]
    public void ComputeEffectiveMultipliers_StacksGroupAndModifier()
    {
        var bands = Bands((2, 4, 0.9m, 1.1m));
        var rule = new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = Guid.NewGuid(),
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = [new ActivityModifierRuleSnapshotDto { CapabilityKey = "cap", TimeMultiplier = 0.8m, ProbabilityMultiplier = 1.2m }]
        };
        var caps = new HashSet<string>(StringComparer.Ordinal) { "cap" };

        var (effectiveTime, effectiveProb) = GroupScalingBandSelector.ComputeEffectiveMultipliers(bands, 3, rule, caps);

        effectiveTime.Should().Be(0.9m * 0.8m);
        effectiveProb.Should().Be(1.1m * 1.2m);
    }
}
