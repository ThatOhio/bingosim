namespace BingoSim.Application.DTOs;

/// <summary>
/// DTO for activity mode support (solo/group and size bounds).
/// </summary>
public record ActivityModeSupportDto(
    bool SupportsSolo,
    bool SupportsGroup,
    int? MinGroupSize,
    int? MaxGroupSize);
