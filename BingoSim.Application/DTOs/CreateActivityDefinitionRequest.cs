namespace BingoSim.Application.DTOs;

/// <summary>
/// Request DTO for creating a new ActivityDefinition.
/// </summary>
public record CreateActivityDefinitionRequest(
    string Key,
    string Name,
    ActivityModeSupportDto ModeSupport,
    List<ActivityAttemptDefinitionDto> Attempts,
    List<GroupSizeBandDto> GroupScalingBands);
