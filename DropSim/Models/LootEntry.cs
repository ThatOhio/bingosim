using DropSim.Util;

namespace DropSim.Models;

public abstract class LootEntry
{
    // Weight used for weighted selection. Recommended to be a probability (e.g., 1/115),
    // but any positive weights are acceptable.
    public double Weight { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    protected LootEntry(double weight, string displayName)
    {
        Weight = weight;
        DisplayName = displayName;
    }

    // Resolve the chosen entry into concrete item stacks
    public abstract List<ItemStack> Resolve(Rng rng);
}

public sealed class ItemLootEntry : LootEntry
{
    public string ItemName { get; }
    public int QuantityMin { get; }
    public int QuantityMax { get; }

    public ItemLootEntry(string itemName, double weight, int quantity = 1)
        : this(itemName, weight, quantity, quantity)
    {
    }

    public ItemLootEntry(string itemName, double weight, int quantityMin, int quantityMax)
        : base(weight, itemName)
    {
        ItemName = itemName;
        QuantityMin = Math.Max(1, quantityMin);
        QuantityMax = Math.Max(QuantityMin, quantityMax);
    }

    public override List<ItemStack> Resolve(Rng rng)
    {
        int qty = QuantityMin == QuantityMax ? QuantityMin : rng.NextInt(QuantityMin, QuantityMax + 1);
        return new List<ItemStack> { new ItemStack(ItemName, qty) };
    }
}

public sealed class CompositeLootEntry : LootEntry
{
    public sealed record Option(string Name, List<ItemStack> Items, double Weight = 1.0)
    {
        public override string ToString() => Name;
    }

    private readonly List<Option> _options;

    public CompositeLootEntry(string name, double weight, IEnumerable<Option> options)
        : base(weight, name)
    {
        _options = options.ToList();
        if (_options.Count == 0) throw new ArgumentException("Composite entry must have at least one option", nameof(options));
    }

    public override List<ItemStack> Resolve(Rng rng)
    {
        // Weighted choose one option
        double total = _options.Sum(o => Math.Max(0, o.Weight));
        if (total <= 0) return _options[0].Items; // fallback
        double r = rng.NextDouble() * total;
        double cum = 0;
        foreach (var opt in _options)
        {
            cum += Math.Max(0, opt.Weight);
            if (r <= cum)
            {
                // Return a clone to avoid accidental external mutation
                return opt.Items.Select(i => new ItemStack(i.Name, i.Quantity)).ToList();
            }
        }
        return _options[^1].Items.Select(i => new ItemStack(i.Name, i.Quantity)).ToList();
    }
}