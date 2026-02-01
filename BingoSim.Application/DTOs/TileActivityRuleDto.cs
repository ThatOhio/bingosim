namespace BingoSim.Application.DTOs;

/// <summary>
/// DTO for a tile activity rule. ActivityDefinitionId is source of truth; ActivityKey is persisted for display/debug.
/// </summary>
public record TileActivityRuleDto(
    Guid ActivityDefinitionId,
    string ActivityKey,
    string? ActivityName,
    List<string> AcceptedDropKeys,
    List<CapabilityDto> Requirements,
    List<ActivityModifierRuleDto> Modifiers);
