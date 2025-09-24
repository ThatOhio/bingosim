namespace BingoSim.Models;

public class Board
{
    public List<Activity> Activities { get; set; } = new();
    public List<Row> Rows { get; set; } = new();

    // Returns whether a tile is currently unlocked
    public bool IsTileUnlocked(Tile tile)
    {
        if (tile.RowIndex == 0) return true; // first row always unlocked at start
        // A row N is unlocked when previous row (N-1) has >= 5 points completed
        var prevRow = Rows.First(r => r.Index == tile.RowIndex - 1);
        return prevRow.PointsCompleted >= 5;
    }

    public IEnumerable<Tile> AllTiles() => Rows.SelectMany(r => r.Tiles);

    public Activity? GetActivity(string id) => Activities.FirstOrDefault(a => a.Id == id);

    public void ResetProgress()
    {
        foreach (var t in AllTiles()) t.ResetProgress();
    }

    // Create a deep clone of this board suitable for an independent simulation run
    public Board DeepClone()
    {
        var clone = new Board();
        // Activities are immutable in simulation; ok to share references
        clone.Activities = Activities;
        // Deep copy rows and tiles (mutable state lives here)
        clone.Rows = Rows.Select(r => new Row
        {
            Index = r.Index,
            Tiles = r.Tiles.Select(t => new Tile
            {
                Id = t.Id,
                RowIndex = t.RowIndex,
                Difficulty = t.Difficulty,
                ActivityId = t.ActivityId,
                ItemsNeeded = t.ItemsNeeded,
                DropChancePerAttempt = t.DropChancePerAttempt,
                AvgTimePerAttemptMinutes = t.AvgTimePerAttemptMinutes,
                Sources = t.Sources.Select(s => new ProgressSource
                {
                    Name = s.Name,
                    RollsPerAttempt = s.RollsPerAttempt,
                    ChancePerRoll = s.ChancePerRoll,
                    QuantityPerSuccess = s.QuantityPerSuccess
                }).ToList()
            }).ToList()
        }).ToList();
        return clone;
    }
}
