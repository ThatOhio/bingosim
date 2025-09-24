namespace BingoSim.Models;

public class Row
{
    public int Index { get; set; }
    public List<Tile> Tiles { get; set; } = new();

    // Points earned by completed tiles in this row
    public int PointsCompleted => Tiles.Where(t => t.Completed).Sum(t => t.Points);
}
