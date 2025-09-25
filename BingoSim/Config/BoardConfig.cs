using System.Text.Json;
using System.Text.Json.Serialization;
using BingoSim.Models;

namespace BingoSim.Config;

public class BoardConfig
{
    public List<Activity> Activities { get; set; } = new();
    public List<RowConfig> Rows { get; set; } = new();

    public static Board LoadFromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        var cfg = JsonSerializer.Deserialize<BoardConfig>(json, options) ?? new BoardConfig();
        return cfg.ToBoard();
    }

    public static Board LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    public Board ToBoard()
    {
        var board = new Board
        {
            Activities = Activities,
            Rows = Rows.OrderBy(r => r.Index).Select(r => r.ToRow()).ToList()
        };
        return board;
    }
}

public class RowConfig
{
    public int Index { get; set; }
    public List<TileConfig> Tiles { get; set; } = new();

    public Row ToRow()
    {
        return new Row
        {
            Index = Index,
            Tiles = Tiles.Select(t => t.ToTile(Index)).ToList()
        };
    }
}

public class ProgressSourceConfig
{
    public string Name { get; set; } = string.Empty;
    public int RollsPerAttempt { get; set; } = 1;
    public double ChancePerRoll { get; set; } = 0.0;
    public int QuantityPerSuccess { get; set; } = 1;

    public ProgressSource ToSource()
    {
        return new ProgressSource
        {
            Name = Name,
            RollsPerAttempt = RollsPerAttempt,
            ChancePerRoll = ChancePerRoll,
            QuantityPerSuccess = QuantityPerSuccess
        };
    }
}

public class TileConfig
{
    public string Id { get; set; } = string.Empty;
    public TileDifficulty Difficulty { get; set; } = TileDifficulty.Easy;
    public string ActivityId { get; set; } = string.Empty;
    public int ItemsNeeded { get; set; } = 1;
    public double DropChancePerAttempt { get; set; } = 0.05;
    // Legacy minutes field (supported for backward compatibility)
    public double? AvgTimePerAttemptMinutes { get; set; }
    // New seconds field
    public double? AvgTimePerAttemptSeconds { get; set; }
    public List<ProgressSourceConfig>? Sources { get; set; }

    public Tile ToTile(int rowIndex)
    {
        var tile = new Tile
        {
            Id = Id,
            RowIndex = rowIndex,
            Difficulty = Difficulty,
            ActivityId = ActivityId,
            ItemsNeeded = ItemsNeeded,
            DropChancePerAttempt = DropChancePerAttempt
        };

        // Map time: prefer explicit seconds; else convert minutes to seconds; default 120s
        if (AvgTimePerAttemptSeconds.HasValue)
        {
            tile.AvgTimePerAttemptSeconds = AvgTimePerAttemptSeconds.Value;
        }
        else if (AvgTimePerAttemptMinutes.HasValue)
        {
            tile.AvgTimePerAttemptSeconds = AvgTimePerAttemptMinutes.Value * 60.0;
        }
        else
        {
            tile.AvgTimePerAttemptSeconds = 120.0;
        }

        if (Sources != null && Sources.Count > 0)
        {
            tile.Sources = Sources.Select(s => s.ToSource()).ToList();
        }
        else
        {
            // Create a default single-source equivalent to the legacy fields
            tile.Sources = new List<ProgressSource>
            {
                new ProgressSource
                {
                    Name = "default",
                    RollsPerAttempt = 1,
                    ChancePerRoll = DropChancePerAttempt,
                    QuantityPerSuccess = 1
                }
            };
        }
        return tile;
    }
}
