namespace BingoSim.Application.Simulation.Strategies;

/// <summary>
/// Calculates all valid tile combinations that meet or exceed the point threshold for row unlock.
/// Uses backtracking to enumerate subsets. Time complexity O(2^n) in worst case for n tiles.
/// Caching results per row is recommended since row structure is static during a simulation run.
/// </summary>
/// <remarks>
/// <para><b>Algorithm:</b> Backtracking over tiles in fixed order (by points, then key). For each tile, we include or skip.
/// A combination is recorded only when including a tile pushes the sum to >= threshold, avoiding duplicates from the skip-then-recurse path.</para>
/// <para><b>Validation:</b> Each combination has TotalPoints >= threshold. Tiles are ordered by points ascending for pruning efficiency.</para>
/// <para><b>Caching:</b> RowUnlockingStrategy caches results per row index. Cache key is row index; invalidation is not needed
/// since row structure does not change during a simulation run.</para>
/// </remarks>
public static class RowCombinationCalculator
{
    private const int MaxTilesPerCombination = 8;

    /// <summary>
    /// Calculates all valid tile combinations that meet or exceed the point threshold.
    /// Combinations are built by processing tiles in order to avoid duplicates (e.g., [A,B] but not [B,A]).
    /// </summary>
    /// <param name="tiles">Dictionary of tile key to tile points.</param>
    /// <param name="threshold">Point threshold to unlock next row (typically 5).</param>
    /// <returns>List of valid tile combinations. Empty if no tiles or threshold not achievable.</returns>
    public static List<TileCombination> CalculateCombinations(
        IReadOnlyDictionary<string, int> tiles,
        int threshold)
    {
        if (tiles.Count == 0 || threshold <= 0)
            return [];

        var tileList = tiles
            .OrderBy(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .ToList();

        var results = new List<TileCombination>();
        var current = new List<string>();
        FindCombinations(tileList, threshold, 0, current, 0, results);
        return results;
    }

    private static void FindCombinations(
        List<KeyValuePair<string, int>> tiles,
        int threshold,
        int currentIndex,
        List<string> currentCombination,
        int currentSum,
        List<TileCombination> results)
    {
        if (currentIndex < 0 || currentIndex >= tiles.Count || currentCombination.Count >= MaxTilesPerCombination)
            return;

        var (key, points) = tiles[currentIndex];

        currentCombination.Add(key);
        var newSum = currentSum + points;
        if (newSum >= threshold)
        {
            results.Add(new TileCombination
            {
                TileKeys = currentCombination.ToList(),
                TotalPoints = newSum
            });
        }
        FindCombinations(tiles, threshold, currentIndex + 1, currentCombination, newSum, results);
        currentCombination.RemoveAt(currentCombination.Count - 1);

        FindCombinations(tiles, threshold, currentIndex + 1, currentCombination, currentSum, results);
    }
}
