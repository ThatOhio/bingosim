namespace BingoSim.Application.Simulation.Allocation;

/// <summary>
/// Factory that returns ITeamStrategy by strategy key (RowUnlocking).
/// </summary>
public interface ITeamStrategyFactory
{
    /// <summary>
    /// Returns the team strategy for the given strategy key.
    /// </summary>
    ITeamStrategy GetStrategy(string strategyKey);
}
