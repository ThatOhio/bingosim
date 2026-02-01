using System.Text.Json.Serialization;

namespace BingoSim.Core.ValueObjects;

/// <summary>
/// Represents an ordered row of tiles. Each row contains exactly 4 tiles with Points 1, 2, 3, 4.
/// </summary>
public sealed record Row
{
    public int Index { get; init; }
    /// <summary>Private set for System.Text.Json deserialization.</summary>
    [JsonInclude]
    public List<Tile> Tiles { get; private set; } = [];

    public Row(int index, IEnumerable<Tile> tiles)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Row index cannot be negative.");

        ArgumentNullException.ThrowIfNull(tiles);

        var tileList = tiles.ToList();
        if (tileList.Count != 4)
            throw new ArgumentException("Row must contain exactly 4 tiles.", nameof(tiles));

        var points = tileList.Select(t => t.Points).OrderBy(p => p).ToList();
        var expected = new[] { 1, 2, 3, 4 };
        if (!points.SequenceEqual(expected))
            throw new ArgumentException("Row must contain exactly one tile per point value 1, 2, 3, 4.", nameof(tiles));

        Index = index;
        Tiles = tileList;
    }

    /// <summary>
    /// Parameterless constructor for EF Core / System.Text.Json deserialization.
    /// </summary>
    [JsonConstructor]
    private Row()
    {
    }
}
