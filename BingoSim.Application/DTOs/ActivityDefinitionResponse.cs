namespace BingoSim.Application.DTOs;

/// <summary>
/// Response DTO for an ActivityDefinition.
/// </summary>
public record ActivityDefinitionResponse(
    Guid Id,
    string Key,
    string Name,
    ActivityModeSupportDto ModeSupport,
    List<ActivityAttemptDefinitionDto> Attempts,
    List<GroupSizeBandDto> GroupScalingBands,
    DateTimeOffset CreatedAt);
