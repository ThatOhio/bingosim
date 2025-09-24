using BingoSim.Models;

namespace BingoSim.Strategies;

public class GroupedByActivityStrategy : IStrategy
{
    public string Name => "grouped";

    public List<Tile> ChooseTargets(Board board)
    {
        // Select the activity that has the highest total points among unlocked, incomplete tiles
        var unlocked = board.AllTiles().Where(t => !t.Completed && board.IsTileUnlocked(t)).ToList();
        if (unlocked.Count == 0) return new List<Tile>();

        var byActivity = unlocked.GroupBy(t => t.ActivityId)
            .Select(g => new {
                ActivityId = g.Key,
                TotalPoints = g.Sum(t => t.Points),
                Tiles = g.OrderBy(t => t.RowIndex).ToList()
            })
            .OrderByDescending(x => x.TotalPoints)
            .ThenBy(x => x.Tiles.Min(t => t.RowIndex))
            .First();

        return byActivity.Tiles;
    }
}
