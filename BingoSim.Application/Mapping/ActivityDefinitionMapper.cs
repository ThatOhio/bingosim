using BingoSim.Application.DTOs;
using BingoSim.Core.Entities;
using BingoSim.Core.Enums;
using BingoSim.Core.ValueObjects;

namespace BingoSim.Application.Mapping;

/// <summary>
/// Manual mapping between ActivityDefinition entities and DTOs.
/// </summary>
public static class ActivityDefinitionMapper
{
    public static ActivityDefinitionResponse ToResponse(ActivityDefinition entity)
    {
        return new ActivityDefinitionResponse(
            Id: entity.Id,
            Key: entity.Key,
            Name: entity.Name,
            ModeSupport: ToDto(entity.ModeSupport),
            Attempts: entity.Attempts.Select(ToDto).ToList(),
            GroupScalingBands: entity.GroupScalingBands.Select(ToDto).ToList(),
            CreatedAt: entity.CreatedAt);
    }

    public static ActivityModeSupportDto ToDto(ActivityModeSupport vo)
    {
        return new ActivityModeSupportDto(
            vo.SupportsSolo,
            vo.SupportsGroup,
            vo.MinGroupSize,
            vo.MaxGroupSize);
    }

    public static AttemptTimeModelDto ToDto(AttemptTimeModel vo)
    {
        return new AttemptTimeModelDto(
            vo.BaselineTimeSeconds,
            (int)vo.Distribution,
            vo.VarianceSeconds);
    }

    public static ProgressGrantDto ToDto(ProgressGrant vo)
    {
        return vo.IsVariable
            ? new ProgressGrantDto(vo.DropKey, vo.Units, vo.UnitsMin, vo.UnitsMax)
            : new ProgressGrantDto(vo.DropKey, vo.Units);
    }

    public static ActivityOutcomeDefinitionDto ToDto(ActivityOutcomeDefinition vo)
    {
        return new ActivityOutcomeDefinitionDto(
            vo.Key,
            vo.WeightNumerator,
            vo.WeightDenominator,
            vo.Grants.Select(ToDto).ToList());
    }

    public static ActivityAttemptDefinitionDto ToDto(ActivityAttemptDefinition vo)
    {
        return new ActivityAttemptDefinitionDto(
            vo.Key,
            (int)vo.RollScope,
            ToDto(vo.TimeModel),
            vo.Outcomes.Select(ToDto).ToList());
    }

    public static GroupSizeBandDto ToDto(GroupSizeBand vo)
    {
        return new GroupSizeBandDto(
            vo.MinSize,
            vo.MaxSize,
            vo.TimeMultiplier,
            vo.ProbabilityMultiplier);
    }

    public static ActivityModeSupport ToEntity(ActivityModeSupportDto dto)
    {
        return new ActivityModeSupport(
            dto.SupportsSolo,
            dto.SupportsGroup,
            dto.MinGroupSize,
            dto.MaxGroupSize);
    }

    public static AttemptTimeModel ToEntity(AttemptTimeModelDto dto)
    {
        return new AttemptTimeModel(
            dto.BaselineTimeSeconds,
            (TimeDistribution)dto.Distribution,
            dto.VarianceSeconds);
    }

    public static ProgressGrant ToEntity(ProgressGrantDto dto)
    {
        if (dto.UnitsMin.HasValue && dto.UnitsMax.HasValue)
            return new ProgressGrant(dto.DropKey, dto.UnitsMin.Value, dto.UnitsMax.Value);
        return new ProgressGrant(dto.DropKey, dto.Units);
    }

    public static ActivityOutcomeDefinition ToEntity(ActivityOutcomeDefinitionDto dto)
    {
        var grants = dto.Grants?.Select(ToEntity).ToList() ?? [];
        return new ActivityOutcomeDefinition(dto.Key, dto.WeightNumerator, dto.WeightDenominator, grants);
    }

    public static ActivityAttemptDefinition ToEntity(ActivityAttemptDefinitionDto dto)
    {
        var outcomes = dto.Outcomes?.Select(ToEntity).ToList() ?? [];
        if (outcomes.Count == 0)
            throw new ArgumentException("At least one outcome is required.", nameof(dto));

        return new ActivityAttemptDefinition(
            dto.Key,
            (RollScope)dto.RollScope,
            ToEntity(dto.TimeModel),
            outcomes);
    }

    public static GroupSizeBand ToEntity(GroupSizeBandDto dto)
    {
        return new GroupSizeBand(dto.MinSize, dto.MaxSize, dto.TimeMultiplier, dto.ProbabilityMultiplier);
    }
}
