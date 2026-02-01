namespace BingoSim.Application.DTOs;

/// <summary>
/// DTO for an activity modifier rule (capability + optional time/probability multipliers).
/// </summary>
public record ActivityModifierRuleDto(CapabilityDto Capability, decimal? TimeMultiplier, decimal? ProbabilityMultiplier);
