using DropSim.Util;

namespace DropSim.Models;

public class TertiaryDrop
{
    public string ItemName { get; }
    public int QuantityMin { get; }
    public int QuantityMax { get; }
    public double Probability { get; }

    public TertiaryDrop(string itemName, double probability, int quantity = 1)
        : this(itemName, probability, quantity, quantity)
    {
    }

    public TertiaryDrop(string itemName, double probability, int quantityMin, int quantityMax)
    {
        ItemName = itemName;
        QuantityMin = Math.Max(1, quantityMin);
        QuantityMax = Math.Max(QuantityMin, quantityMax);
        Probability = Math.Clamp(probability, 0, 1);
    }

    public ItemStack? TryRoll(Rng rng)
    {
        if (!rng.Chance(Probability)) return null;
        int qty = QuantityMin == QuantityMax ? QuantityMin : rng.NextInt(QuantityMin, QuantityMax + 1);
        return new ItemStack(ItemName, qty);
    }
}