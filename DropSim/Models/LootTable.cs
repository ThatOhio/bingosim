using DropSim.Util;

namespace DropSim.Models;

public class LootTable
{
    private readonly List<LootEntry> _entries = new();

    public string Name { get; }

    // Number of times this table is rolled per boss kill (e.g., 2 for two main drops)
    public int RollsPerKill { get; init; } = 1;

    public LootTable(string name)
    {
        Name = name;
    }

    public LootTable Add(LootEntry entry)
    {
        _entries.Add(entry);
        return this;
    }

    public IReadOnlyList<LootEntry> Entries => _entries;

    public List<ItemStack> RollOnce(Rng rng)
    {
        if (_entries.Count == 0) return new List<ItemStack>();
        double totalWeight = _entries.Sum(e => Math.Max(0, e.Weight));
        if (totalWeight <= 0)
        {
            // Fallback: return first entry
            return _entries[0].Resolve(rng);
        }
        double r = rng.NextDouble() * totalWeight;
        double cumulative = 0;
        foreach (var e in _entries)
        {
            cumulative += Math.Max(0, e.Weight);
            if (r <= cumulative)
            {
                return e.Resolve(rng);
            }
        }
        return _entries[^1].Resolve(rng);
    }
}
