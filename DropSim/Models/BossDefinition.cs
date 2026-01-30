namespace DropSim.Models;

public class BossDefinition
{
    public string Name { get; }

    // Always received
    public List<ItemStack> GuaranteedDrops { get; } = new();

    // Main loot tables and how many rolls per kill
    public List<(LootTable Table, int Rolls)> MainLootTables { get; } = new();

    // Independently rolled tertiary drops
    public List<TertiaryDrop> TertiaryDrops { get; } = new();

    public BossDefinition(string name)
    {
        Name = name;
    }

    public BossDefinition AddGuaranteed(ItemStack stack)
    {
        GuaranteedDrops.Add(stack);
        return this;
    }

    public BossDefinition AddMainTable(LootTable table, int rolls = 1)
    {
        MainLootTables.Add((table, Math.Max(1, rolls)));
        return this;
    }

    public BossDefinition AddTertiary(TertiaryDrop tertiary)
    {
        TertiaryDrops.Add(tertiary);
        return this;
    }
}