namespace BingoSim.Application.DTOs;

/// <summary>
/// Request DTO for updating an existing ActivityDefinition.
/// </summary>
public record UpdateActivityDefinitionRequest(
    string Key,
    string Name,
    ActivityModeSupportDto ModeSupport,
    List<ActivityAttemptDefinitionDto> Attempts,
    List<GroupSizeBandDto> GroupScalingBands);
