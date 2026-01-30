namespace DropSim.Models;

public record ItemStack(string Name, int Quantity)
{
    public override string ToString() => Quantity == 1 ? Name : $"{Name} x{Quantity}";
}