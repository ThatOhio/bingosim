using BingoSim.Core.ValueObjects;

namespace BingoSim.Core.Entities;

/// <summary>
/// Represents a reusable player definition that can be selected into an Event Team.
/// </summary>
public class PlayerProfile
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Skill time multiplier (e.g., 0.8 = 20% faster, 1.2 = 20% slower).
    /// Default is 1.0 (no modifier).
    /// </summary>
    public decimal SkillTimeMultiplier { get; private set; } = 1.0m;

    private readonly List<Capability> _capabilities = [];
    public IReadOnlyList<Capability> Capabilities => _capabilities.AsReadOnly();

    public WeeklySchedule WeeklySchedule { get; private set; } = new();

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private PlayerProfile() { }

    public PlayerProfile(string name, decimal skillTimeMultiplier = 1.0m)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        if (skillTimeMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(skillTimeMultiplier), "Skill time multiplier must be greater than zero.");

        Id = Guid.NewGuid();
        Name = name;
        SkillTimeMultiplier = skillTimeMultiplier;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Name = name;
    }

    public void UpdateSkillTimeMultiplier(decimal multiplier)
    {
        if (multiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Skill time multiplier must be greater than zero.");

        SkillTimeMultiplier = multiplier;
    }

    public void AddCapability(Capability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (_capabilities.Any(c => c.Key == capability.Key))
            throw new InvalidOperationException($"Capability with key '{capability.Key}' already exists.");

        _capabilities.Add(capability);
    }

    public void RemoveCapability(string key)
    {
        var capability = _capabilities.FirstOrDefault(c => c.Key == key);
        if (capability is not null)
        {
            _capabilities.Remove(capability);
        }
    }

    public void ClearCapabilities()
    {
        _capabilities.Clear();
    }

    public void SetCapabilities(IEnumerable<Capability> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        _capabilities.Clear();
        _capabilities.AddRange(capabilities);
    }

    public void SetWeeklySchedule(WeeklySchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        WeeklySchedule = schedule;
    }

    public bool HasCapability(string key)
    {
        return _capabilities.Any(c => c.Key == key);
    }
}
