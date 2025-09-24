using System.Text.Json.Serialization;

namespace BingoSim.Models;

public class Tile
{
    public string Id { get; set; } = string.Empty;
    public int RowIndex { get; set; }
    public TileDifficulty Difficulty { get; set; }
    public int Points => (int)Difficulty;

    // Activity linkage
    public string ActivityId { get; set; } = string.Empty;

    // Target amount to complete this tile
    public int ItemsNeeded { get; set; } = 1;

    // Legacy simple model fields (kept for backward compatibility with configs without "sources")
    public double DropChancePerAttempt { get; set; } = 0.05; // probability 0..1 per attempt (legacy)

    // Average time per attempt for the activity
    public double AvgTimePerAttemptMinutes { get; set; } = 2.0;

    // New: one tile can progress from multiple independent sources per attempt (e.g., normal table rolls and rare unique)
    public List<ProgressSource> Sources { get; set; } = new();

    [JsonIgnore]
    public bool Completed { get; set; }

    [JsonIgnore]
    public int ItemsObtained { get; set; }

    public void ResetProgress()
    {
        Completed = false;
        ItemsObtained = 0;
    }

    // Expected units of progress per attempt considering all sources (or legacy chance)
    public double ExpectedUnitsPerAttempt()
    {
        if (Sources != null && Sources.Count > 0)
        {
            double sum = 0;
            foreach (var s in Sources)
            {
                sum += Math.Max(0, s.RollsPerAttempt) * Math.Max(0.0, s.ChancePerRoll) * Math.Max(0, s.QuantityPerSuccess);
            }
            return sum;
        }
        // Legacy: one roll per attempt, quantity 1
        return Math.Max(0.0, DropChancePerAttempt);
    }
}
