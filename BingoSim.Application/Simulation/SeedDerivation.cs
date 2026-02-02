namespace BingoSim.Application.Simulation;

/// <summary>
/// Derives a deterministic RNG seed from batch seed string + run index for reproducibility.
/// </summary>
public static class SeedDerivation
{
    /// <summary>
    /// Derives an integer seed for System.Random from (batchSeedString + runIndex).
    /// Same inputs always produce the same output.
    /// </summary>
    public static int DeriveRngSeed(string batchSeedString, int runIndex)
    {
        ArgumentNullException.ThrowIfNull(batchSeedString);

        var combined = $"{batchSeedString.Trim()}_{runIndex}";
        unchecked
        {
            var hash = 17;
            foreach (var c in combined)
                hash = hash * 31 + c;
            return hash;
        }
    }

    /// <summary>
    /// Derives the run seed string for storage and UI display (batch seed + run index).
    /// </summary>
    public static string DeriveRunSeedString(string batchSeedString, int runIndex)
    {
        ArgumentNullException.ThrowIfNull(batchSeedString);
        return $"{batchSeedString.Trim()}_{runIndex}";
    }
}
