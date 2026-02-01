namespace BingoSim.Application.DTOs;

/// <summary>
/// DTO for attempt time model (baseline, distribution, variance).
/// </summary>
public record AttemptTimeModelDto(
    int BaselineTimeSeconds,
    int Distribution,
    int? VarianceSeconds);
