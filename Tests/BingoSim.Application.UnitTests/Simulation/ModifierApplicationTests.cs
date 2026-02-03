using BingoSim.Application.Simulation;
using BingoSim.Application.Simulation.Snapshot;
using FluentAssertions;
using Xunit;

namespace BingoSim.Application.UnitTests.Simulation;

public class ModifierApplicationTests
{
    [Fact]
    public void ComputeCombinedTimeMultiplier_NoApplicableModifiers_ReturnsOne()
    {
        var rule = CreateRule([("quest.ds2", 0.9m, null)]);
        var caps = new HashSet<string>(StringComparer.Ordinal) { "other.cap" };

        var result = ModifierApplicator.ComputeCombinedTimeMultiplier(rule, caps);

        result.Should().Be(1.0m);
    }

    [Fact]
    public void ComputeCombinedTimeMultiplier_SingleApplicableTimeModifier_ReturnsMultiplier()
    {
        var rule = CreateRule([("quest.ds2", 0.9m, null)]);
        var caps = new HashSet<string>(StringComparer.Ordinal) { "quest.ds2" };

        var result = ModifierApplicator.ComputeCombinedTimeMultiplier(rule, caps);

        result.Should().Be(0.9m);
    }

    [Fact]
    public void ComputeCombinedTimeMultiplier_TwoApplicableModifiers_MultipliersMultiply()
    {
        var rule = CreateRule([("quest.ds2", 0.9m, null), ("item.dhl", 0.8m, null)]);
        var caps = new HashSet<string>(StringComparer.Ordinal) { "quest.ds2", "item.dhl" };

        var result = ModifierApplicator.ComputeCombinedTimeMultiplier(rule, caps);

        result.Should().Be(0.72m);
    }

    [Fact]
    public void ComputeCombinedTimeMultiplier_NullValuesSkipped()
    {
        var rule = CreateRule([("quest.ds2", null, 1.2m)]);
        var caps = new HashSet<string>(StringComparer.Ordinal) { "quest.ds2" };

        var timeResult = ModifierApplicator.ComputeCombinedTimeMultiplier(rule, caps);
        var probResult = ModifierApplicator.ComputeCombinedProbabilityMultiplier(rule, caps);

        timeResult.Should().Be(1.0m);
        probResult.Should().Be(1.2m);
    }

    [Fact]
    public void ComputeCombinedMultipliers_SinglePass_ReturnsBothCorrectly()
    {
        var rule = CreateRule([("quest.ds2", 0.9m, 1.2m)]);
        var caps = new HashSet<string>(StringComparer.Ordinal) { "quest.ds2" };

        var (time, prob) = ModifierApplicator.ComputeCombinedMultipliers(rule, caps);

        time.Should().Be(0.9m);
        prob.Should().Be(1.2m);
    }

    [Fact]
    public void ComputeCombinedTimeMultiplier_NullRule_ReturnsOne()
    {
        var caps = new HashSet<string>(StringComparer.Ordinal) { "quest.ds2" };

        var result = ModifierApplicator.ComputeCombinedTimeMultiplier(null, caps);

        result.Should().Be(1.0m);
    }

    [Fact]
    public void ApplicableModifiers_PlayerHasCapability_ModifierApplies()
    {
        var rule = CreateRule([("quest.ds2", 0.9m, 1.1m)]);
        var caps = new HashSet<string>(StringComparer.Ordinal) { "quest.ds2" };

        var timeResult = ModifierApplicator.ComputeCombinedTimeMultiplier(rule, caps);
        var probResult = ModifierApplicator.ComputeCombinedProbabilityMultiplier(rule, caps);

        timeResult.Should().Be(0.9m);
        probResult.Should().Be(1.1m);
    }

    [Fact]
    public void ApplicableModifiers_PlayerLacksCapability_ModifierExcluded()
    {
        var rule = CreateRule([("item.dhl", 0.85m, 1.05m)]);
        var caps = new HashSet<string>(StringComparer.Ordinal) { "quest.ds2" };

        var timeResult = ModifierApplicator.ComputeCombinedTimeMultiplier(rule, caps);
        var probResult = ModifierApplicator.ComputeCombinedProbabilityMultiplier(rule, caps);

        timeResult.Should().Be(1.0m);
        probResult.Should().Be(1.0m);
    }

    [Fact]
    public void ApplicableModifiers_PartialMatch_OnlyMatchingApply()
    {
        var rule = CreateRule([("a", 0.9m, null), ("b", 0.8m, null)]);
        var caps = new HashSet<string>(StringComparer.Ordinal) { "a" };

        var result = ModifierApplicator.ComputeCombinedTimeMultiplier(rule, caps);

        result.Should().Be(0.9m);
    }

    [Fact]
    public void ApplyProbabilityMultiplier_OutcomeWithMatchingDropKey_ScalesWeight()
    {
        var outcomes = new List<OutcomeSnapshotDto>
        {
            new() { WeightNumerator = 90, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "other", Units = 1 }] },
            new() { WeightNumerator = 10, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }
        };
        var acceptedDropKeys = new HashSet<string>(StringComparer.Ordinal) { "drop" };

        var weights = ModifierApplicator.ApplyProbabilityMultiplier(outcomes, acceptedDropKeys, 2.0m);

        weights.Should().HaveCount(2);
        weights[0].Should().Be(90);
        weights[1].Should().Be(20);
    }

    [Fact]
    public void ApplyProbabilityMultiplier_OutcomeWithNonMatchingDropKey_KeepsWeight()
    {
        var outcomes = new List<OutcomeSnapshotDto>
        {
            new() { WeightNumerator = 90, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "other", Units = 1 }] },
            new() { WeightNumerator = 10, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }
        };
        var acceptedDropKeys = new HashSet<string>(StringComparer.Ordinal) { "drop" };

        var weights = ModifierApplicator.ApplyProbabilityMultiplier(outcomes, acceptedDropKeys, 2.0m);

        weights[0].Should().Be(90);
    }

    [Fact]
    public void ApplyProbabilityMultiplier_MultiplierOne_ReturnsOriginalWeights()
    {
        var outcomes = new List<OutcomeSnapshotDto>
        {
            new() { WeightNumerator = 10, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }
        };
        var acceptedDropKeys = new HashSet<string>(StringComparer.Ordinal) { "drop" };

        var weights = ModifierApplicator.ApplyProbabilityMultiplier(outcomes, acceptedDropKeys, 1.0m);

        weights.Should().HaveCount(1);
        weights[0].Should().Be(10);
    }

    [Fact]
    public void ApplyProbabilityMultiplier_EmptyAcceptedDropKeys_ReturnsOriginalWeights()
    {
        var outcomes = new List<OutcomeSnapshotDto>
        {
            new() { WeightNumerator = 10, WeightDenominator = 1, Grants = [new ProgressGrantSnapshotDto { DropKey = "drop", Units = 1 }] }
        };
        var acceptedDropKeys = new HashSet<string>(StringComparer.Ordinal);

        var weights = ModifierApplicator.ApplyProbabilityMultiplier(outcomes, acceptedDropKeys, 2.0m);

        weights[0].Should().Be(10);
    }

    private static TileActivityRuleSnapshotDto CreateRule(
        (string CapKey, decimal? TimeMult, decimal? ProbMult)[] modifiers)
    {
        return new TileActivityRuleSnapshotDto
        {
            ActivityDefinitionId = Guid.NewGuid(),
            ActivityKey = "act",
            AcceptedDropKeys = ["drop"],
            RequirementKeys = [],
            Modifiers = modifiers.Select(m => new ActivityModifierRuleSnapshotDto
            {
                CapabilityKey = m.CapKey,
                TimeMultiplier = m.TimeMult,
                ProbabilityMultiplier = m.ProbMult
            }).ToList()
        };
    }
}
