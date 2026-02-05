using BingoSim.Application.DTOs;
using BingoSim.Application.Mapping;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.ValueObjects;
using FluentAssertions;

namespace BingoSim.Application.UnitTests.Mapping;

public class ActivityDefinitionMapperTests
{
    [Fact]
    public void ToResponse_Entity_MapsToResponse()
    {
        var entity = CreateTestEntity();

        var result = ActivityDefinitionMapper.ToResponse(entity);

        result.Id.Should().Be(entity.Id);
        result.Key.Should().Be(entity.Key);
        result.Name.Should().Be(entity.Name);
        result.ModeSupport.SupportsSolo.Should().Be(entity.ModeSupport.SupportsSolo);
        result.ModeSupport.SupportsGroup.Should().Be(entity.ModeSupport.SupportsGroup);
        result.Attempts.Should().HaveCount(1);
        result.Attempts[0].Key.Should().Be("attempt_1");
        result.Attempts[0].RollScope.Should().Be(0);
        result.Attempts[0].TimeModel.BaselineTimeSeconds.Should().Be(60);
        result.Attempts[0].Outcomes.Should().HaveCount(1);
        result.Attempts[0].Outcomes[0].Grants.Should().HaveCount(1);
        result.Attempts[0].Outcomes[0].Grants[0].DropKey.Should().Be("drop.key");
        result.Attempts[0].Outcomes[0].Grants[0].Units.Should().Be(1);
        result.GroupScalingBands.Should().HaveCount(1);
        result.GroupScalingBands[0].MinSize.Should().Be(1);
        result.GroupScalingBands[0].MaxSize.Should().Be(4);
        result.CreatedAt.Should().Be(entity.CreatedAt);
    }

    [Fact]
    public void ToEntity_ActivityModeSupportDto_MapsCorrectly()
    {
        var dto = new ActivityModeSupportDto(true, true, 2, 8);
        var result = ActivityDefinitionMapper.ToEntity(dto);
        result.SupportsSolo.Should().BeTrue();
        result.SupportsGroup.Should().BeTrue();
        result.MinGroupSize.Should().Be(2);
        result.MaxGroupSize.Should().Be(8);
    }

    [Fact]
    public void ToEntity_AttemptTimeModelDto_MapsCorrectly()
    {
        var dto = new AttemptTimeModelDto(120, 1, 20);
        var result = ActivityDefinitionMapper.ToEntity(dto);
        result.BaselineTimeSeconds.Should().Be(120);
        result.Distribution.Should().Be(TimeDistribution.NormalApprox);
        result.VarianceSeconds.Should().Be(20);
    }

    [Fact]
    public void ToEntity_ProgressGrantDto_MapsCorrectly()
    {
        var dto = new ProgressGrantDto("drop.magic_fang", 3);
        var result = ActivityDefinitionMapper.ToEntity(dto);
        result.DropKey.Should().Be("drop.magic_fang");
        result.Units.Should().Be(3);
        result.IsVariable.Should().BeFalse();
    }

    [Fact]
    public void ToEntity_ProgressGrantDto_VariableUnits_MapsCorrectly()
    {
        var dto = new ProgressGrantDto("item.arrows", 0, 50, 100);
        var result = ActivityDefinitionMapper.ToEntity(dto);
        result.DropKey.Should().Be("item.arrows");
        result.UnitsMin.Should().Be(50);
        result.UnitsMax.Should().Be(100);
        result.IsVariable.Should().BeTrue();
    }

    [Fact]
    public void ToDto_ProgressGrant_VariableUnits_MapsCorrectly()
    {
        var vo = new ProgressGrant("item.arrows", 50, 100);
        var result = ActivityDefinitionMapper.ToDto(vo);
        result.DropKey.Should().Be("item.arrows");
        result.UnitsMin.Should().Be(50);
        result.UnitsMax.Should().Be(100);
    }

    [Fact]
    public void ToEntity_ActivityAttemptDefinitionDto_MapsCorrectly()
    {
        var dto = new ActivityAttemptDefinitionDto(
            "attempt_1",
            1,
            new AttemptTimeModelDto(90, 0, null),
            [new ActivityOutcomeDefinitionDto("outcome_1", 1, 10, [new ProgressGrantDto("drop.key", 2)])]);
        var result = ActivityDefinitionMapper.ToEntity(dto);
        result.Key.Should().Be("attempt_1");
        result.RollScope.Should().Be(RollScope.PerGroup);
        result.TimeModel.BaselineTimeSeconds.Should().Be(90);
        result.Outcomes.Should().HaveCount(1);
        result.Outcomes[0].Key.Should().Be("outcome_1");
        result.Outcomes[0].Grants[0].DropKey.Should().Be("drop.key");
        result.Outcomes[0].Grants[0].Units.Should().Be(2);
    }

    [Fact]
    public void ToEntity_GroupSizeBandDto_MapsCorrectly()
    {
        var dto = new GroupSizeBandDto(2, 4, 0.9m, 1.1m);
        var result = ActivityDefinitionMapper.ToEntity(dto);
        result.MinSize.Should().Be(2);
        result.MaxSize.Should().Be(4);
        result.TimeMultiplier.Should().Be(0.9m);
        result.ProbabilityMultiplier.Should().Be(1.1m);
    }

    private static ActivityDefinition CreateTestEntity()
    {
        var modeSupport = new ActivityModeSupport(true, true, 1, 8);
        var entity = new ActivityDefinition("activity.zulrah", "Zulrah", modeSupport);
        var attempt = new ActivityAttemptDefinition(
            "attempt_1",
            RollScope.PerPlayer,
            new AttemptTimeModel(60, TimeDistribution.Uniform, 10),
            [new ActivityOutcomeDefinition("outcome_1", 1, 1, [new ProgressGrant("drop.key", 1)])]);
        entity.SetAttempts([attempt]);
        entity.SetGroupScalingBands([new GroupSizeBand(1, 4, 1.0m, 1.0m)]);
        return entity;
    }
}
