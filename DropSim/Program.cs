using DropSim.Models;
using DropSim.Simulation;

static double W(string rate)
{
    // Interpret weight as probability from a string like "1/115"
    return DropSim.Models.DropRate.Parse(rate).Probability;
}

var rngSeed = 42;
var sim = new DropSimulator(seed: rngSeed);

// Example boss: Araxxor (example data for demonstration purposes)
var main = new LootTable("Araxxor Main")
{
    RollsPerKill = 1
}
.Add(new ItemLootEntry("Rune 2h sword", W("1/115"), 1))
.Add(new ItemLootEntry("Araxyte Fang", W("1/600"), 1))
.Add(new CompositeLootEntry(
    name: "Supplies",
    weight: W("1/8"),
    options: new[]
    {
        new CompositeLootEntry.Option("Sharks+Restores",
            new List<ItemStack>
            {
                new("Shark", 12),
                new("Super restore(4)", 3)
            }),
        new CompositeLootEntry.Option("Brews+Saradomin brews",
            new List<ItemStack>
            {
                new("Saradomin brew(4)", 4),
                new("Super restore(4)", 4)
            }),
        new CompositeLootEntry.Option("Karambwans+Stamina",
            new List<ItemStack>
            {
                new("Cooked karambwan", 15),
                new("Stamina potion(4)", 2)
            })
    }
));

var boss = new BossDefinition("Araxxor")
    .AddGuaranteed(new ItemStack("Bones", 1))
    .AddMainTable(main, rolls: 1)
    .AddTertiary(new TertiaryDrop("Araxxor pet", probability: DropRate.FromXOverY(1, 5000).Probability));

int kills = 100;
for (int i = 1; i <= kills; i++)
{
    var drops = sim.SimulateKill(boss);
    Console.WriteLine($"Kill #{i}: {string.Join(", ", drops.Select(d => d.ToString()))}");
}
