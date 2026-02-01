namespace BingoSim.Application.DTOs;

/// <summary>
/// DTO for an activity attempt (loot line) with time model and outcomes.
/// </summary>
public record ActivityAttemptDefinitionDto(
    string Key,
    int RollScope,
    AttemptTimeModelDto TimeModel,
    List<ActivityOutcomeDefinitionDto> Outcomes);
