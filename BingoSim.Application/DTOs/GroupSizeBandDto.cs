namespace BingoSim.Application.DTOs;

/// <summary>
/// DTO for a group size band (min/max size and multipliers).
/// </summary>
public record GroupSizeBandDto(
    int MinSize,
    int MaxSize,
    decimal TimeMultiplier,
    decimal ProbabilityMultiplier);
