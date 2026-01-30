namespace DropSim.Util;

public class Rng
{
    private readonly Random _random;

    public Rng(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
    }

    public bool Chance(double p)
    {
        if (p <= 0) return false;
        if (p >= 1) return true;
        return _random.NextDouble() < p;
    }

    public double NextDouble()
    {
        return _random.NextDouble();
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        return _random.Next(minInclusive, maxExclusive);
    }
}