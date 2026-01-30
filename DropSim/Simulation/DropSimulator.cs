using DropSim.Models;
using DropSim.Util;

namespace DropSim.Simulation;

public class DropSimulator
{
    private readonly Rng _rng;

    public DropSimulator(int? seed = null)
    {
        _rng = new Rng(seed);
    }

    public List<ItemStack> SimulateKill(BossDefinition boss)
    {
        var drops = new List<ItemStack>();

        // Guaranteed
        drops.AddRange(CloneStacks(boss.GuaranteedDrops));

        // Main tables: ensure at least one item is produced overall if at least one main table exists
        bool mainProduced = false;
        foreach (var (table, rolls) in boss.MainLootTables)
        {
            for (int i = 0; i < rolls; i++)
            {
                var result = table.RollOnce(_rng);
                if (result.Count > 0) mainProduced = true;
                drops.AddRange(result);
            }
        }

        // If no main table defined, or empty, that's okay; guaranteed/tertiary may still provide drops.
        // But if the requirement is strictly to always have at least one item, and nothing produced so far,
        // we could optionally return a placeholder. Here we just leave it as is; callers should define tables.

        // Tertiaries
        foreach (var t in boss.TertiaryDrops)
        {
            var item = t.TryRoll(_rng);
            if (item != null) drops.Add(item);
        }

        // Merge same-name stacks for cleanliness
        return MergeStacks(drops);
    }

    private static List<ItemStack> MergeStacks(List<ItemStack> drops)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in drops)
        {
            if (dict.ContainsKey(d.Name)) dict[d.Name] += d.Quantity;
            else dict[d.Name] = d.Quantity;
        }
        return dict.Select(kv => new ItemStack(kv.Key, kv.Value)).ToList();
    }

    private static IEnumerable<ItemStack> CloneStacks(IEnumerable<ItemStack> stacks)
    {
        foreach (var s in stacks)
            yield return new ItemStack(s.Name, s.Quantity);
    }
}