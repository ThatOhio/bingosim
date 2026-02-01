using BingoSim.Core.ValueObjects;

namespace BingoSim.Core.Entities;

/// <summary>
/// Represents an in-game activity (boss, raid, skilling method, etc.) that may be referenced by multiple tiles.
/// </summary>
public class ActivityDefinition
{
    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public ActivityModeSupport ModeSupport { get; private set; } = null!;

    private readonly List<ActivityAttemptDefinition> _attempts = [];
    public IReadOnlyList<ActivityAttemptDefinition> Attempts => _attempts.AsReadOnly();

    private readonly List<GroupSizeBand> _groupScalingBands = [];
    public IReadOnlyList<GroupSizeBand> GroupScalingBands => _groupScalingBands.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private ActivityDefinition() { }

    public ActivityDefinition(string key, string name, ActivityModeSupport modeSupport)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        ArgumentNullException.ThrowIfNull(modeSupport);

        Id = Guid.NewGuid();
        Key = key.Trim();
        Name = name.Trim();
        ModeSupport = modeSupport;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty.", nameof(key));

        Key = key.Trim();
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Name = name.Trim();
    }

    public void SetModeSupport(ActivityModeSupport modeSupport)
    {
        ArgumentNullException.ThrowIfNull(modeSupport);
        ModeSupport = modeSupport;
    }

    public void SetAttempts(IEnumerable<ActivityAttemptDefinition> attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);

        var list = attempts.ToList();
        if (list.Count == 0)
            throw new ArgumentException("At least one attempt definition is required.", nameof(attempts));

        var keys = list.Select(a => a.Key).ToList();
        if (keys.Distinct().Count() != keys.Count)
            throw new InvalidOperationException("Attempt keys must be unique within the activity.");

        _attempts.Clear();
        _attempts.AddRange(list);
    }

    public void SetGroupScalingBands(IEnumerable<GroupSizeBand> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);
        _groupScalingBands.Clear();
        _groupScalingBands.AddRange(bands);
    }
}
