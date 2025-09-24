using BingoSim.Models;

namespace BingoSim.Strategies;

public interface IStrategy
{
    string Name { get; }
    // Return tiles to target in the current step (should be from the same activity)
    // Only tiles that are unlocked and not completed should be returned.
    List<Tile> ChooseTargets(Board board);
}
