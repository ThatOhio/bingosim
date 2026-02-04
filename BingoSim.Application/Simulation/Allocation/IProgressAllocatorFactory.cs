namespace BingoSim.Application.Simulation.Allocation;

/// <summary>
/// Factory that returns ITeamStrategy by strategy key (RowRush, GreedyPoints).
/// </summary>
public interface IProgressAllocatorFactory
{
    ITeamStrategy GetAllocator(string strategyKey);
}
