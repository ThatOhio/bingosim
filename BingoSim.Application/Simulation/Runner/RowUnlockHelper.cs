namespace BingoSim.Application.Simulation.Runner;

/// <summary>
/// Domain rule: row N unlocks when sum of completed tile points in row N-1 >= UnlockPointsRequiredPerRow.
/// Row 0 is unlocked at sim time 0.
/// </summary>
public static class RowUnlockHelper
{
    /// <summary>
    /// Returns the set of row indices that are unlocked given completed tile points per row.
    /// completedPointsByRow[rowIndex] = sum of points of completed tiles in that row.
    /// </summary>
    public static IReadOnlySet<int> ComputeUnlockedRows(
        int unlockPointsRequiredPerRow,
        IReadOnlyDictionary<int, int> completedPointsByRow,
        int totalRowCount)
    {
        var unlocked = new HashSet<int> { 0 };
        for (var row = 1; row < totalRowCount; row++)
        {
            var prevRow = row - 1;
            var completedInPrev = completedPointsByRow.GetValueOrDefault(prevRow, 0);
            if (completedInPrev >= unlockPointsRequiredPerRow)
                unlocked.Add(row);
        }
        return unlocked;
    }
}
