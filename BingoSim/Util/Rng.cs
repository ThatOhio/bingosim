namespace BingoSim.Util;

public class Rng
{
    private readonly Random _random;

    public Rng(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public double NextDouble() => _random.NextDouble();

    public bool Chance(double p)
    {
        if (p <= 0) return false;
        if (p >= 1) return true;
        return _random.NextDouble() < p;
    }

    public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}
