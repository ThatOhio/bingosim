namespace BingoSim.Application.Simulation.Runner;

/// <summary>
/// Mutable per-team state during a simulation run.
/// </summary>
internal sealed class TeamRunState
{
    public Guid TeamId { get; }
    public string TeamName { get; }
    public string StrategyKey { get; }
    public string? ParamsJson { get; }
    public int TotalRowCount { get; }

    private readonly Dictionary<string, int> _tileProgress = new(StringComparer.Ordinal);
    private readonly HashSet<string> _completedTiles = new(StringComparer.Ordinal);
    private readonly Dictionary<int, int> _completedPointsByRow = new();
    private readonly Dictionary<int, int> _rowUnlockTimes = new();
    private readonly Dictionary<string, int> _tileCompletionTimes = new(StringComparer.Ordinal);
    private readonly HashSet<int> _unlockedRowIndices = [0];
    private bool _unlockedRowsDirty = false;

    public IReadOnlySet<int> UnlockedRowIndices
    {
        get
        {
            if (_unlockedRowsDirty)
            {
                _unlockedRowIndices.Clear();
                _unlockedRowIndices.Add(0);
                for (var row = 1; row < TotalRowCount; row++)
                {
                    var prevRow = row - 1;
                    var completedInPrev = _completedPointsByRow.GetValueOrDefault(prevRow, 0);
                    if (completedInPrev >= UnlockPointsRequiredPerRow)
                        _unlockedRowIndices.Add(row);
                }
                _unlockedRowsDirty = false;
            }
            return _unlockedRowIndices;
        }
    }
    public IReadOnlyDictionary<string, int> TileProgress => _tileProgress;
    public IReadOnlySet<string> CompletedTiles => _completedTiles;
    public IReadOnlyDictionary<int, int> RowUnlockTimes => _rowUnlockTimes;
    public IReadOnlyDictionary<string, int> TileCompletionTimes => _tileCompletionTimes;
    public int TotalPoints => _completedTiles.Sum(t => _tilePoints.GetValueOrDefault(t, 0));
    public int TilesCompletedCount => _completedTiles.Count;
    public int RowReached => UnlockedRowIndices.Count > 0 ? UnlockedRowIndices.Max() : 0;

    private int UnlockPointsRequiredPerRow { get; }
    private readonly IReadOnlyDictionary<string, int> _tileRowIndex;
    private readonly IReadOnlyDictionary<string, int> _tilePoints;

    public TeamRunState(
        Guid teamId,
        string teamName,
        string strategyKey,
        string? paramsJson,
        int totalRowCount,
        IReadOnlyDictionary<string, int> tileRowIndex,
        IReadOnlyDictionary<string, int> tilePoints,
        IReadOnlyDictionary<string, int> tileRequiredCount,
        int unlockPointsRequiredPerRow)
    {
        TeamId = teamId;
        TeamName = teamName;
        StrategyKey = strategyKey;
        ParamsJson = paramsJson;
        TotalRowCount = totalRowCount;
        UnlockPointsRequiredPerRow = unlockPointsRequiredPerRow;
        _tileRowIndex = tileRowIndex;
        _tilePoints = tilePoints;
        _rowUnlockTimes[0] = 0;
    }

    public void AddProgress(string tileKey, int units, int simTime, int requiredCount, int rowIndex, int points)
    {
        var current = _tileProgress.GetValueOrDefault(tileKey, 0);
        var next = current + units;
        _tileProgress[tileKey] = next;
        if (next >= requiredCount)
        {
            _tileProgress.Remove(tileKey);
            _completedTiles.Add(tileKey);
            _tileCompletionTimes[tileKey] = simTime;
            var row = rowIndex;
            _completedPointsByRow[row] = _completedPointsByRow.GetValueOrDefault(row, 0) + points;
            _unlockedRowsDirty = true;
            var unlocked = UnlockedRowIndices;
            foreach (var r in unlocked)
            {
                if (!_rowUnlockTimes.ContainsKey(r))
                    _rowUnlockTimes[r] = simTime;
            }
        }
    }
}
