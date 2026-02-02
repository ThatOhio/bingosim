namespace BingoSim.Application.Simulation.Allocation;

/// <summary>
/// Factory that returns IProgressAllocator by strategy key (RowRush, GreedyPoints).
/// </summary>
public interface IProgressAllocatorFactory
{
    IProgressAllocator GetAllocator(string strategyKey);
}
