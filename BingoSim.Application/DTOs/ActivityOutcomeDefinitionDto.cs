namespace BingoSim.Application.DTOs;

/// <summary>
/// DTO for an activity outcome (key, weight, grants).
/// </summary>
public record ActivityOutcomeDefinitionDto(
    string Key,
    int WeightNumerator,
    int WeightDenominator,
    List<ProgressGrantDto> Grants);
