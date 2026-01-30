using System.Globalization;

namespace DropSim.Models;

public readonly struct DropRate
{
    // Probability per roll (0..1]
    public double Probability { get; }

    public DropRate(double probability)
    {
        Probability = probability <= 0 ? 0 : probability > 1 ? 1 : probability;
    }

    public static DropRate FromXOverY(int x, int y)
    {
        if (y <= 0) return new DropRate(0);
        return new DropRate((double)x / y);
    }

    public static DropRate Parse(string text)
    {
        // Accept formats like "1/115", "3/128", or decimal "0.05"
        text = text.Trim();
        if (text.Contains('/'))
        {
            var parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2 &&
                int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var num) &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var den) && den != 0)
            {
                return FromXOverY(num, den);
            }
        }
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var p))
        {
            return new DropRate(p);
        }
        throw new FormatException($"Invalid drop rate: '{text}'");
    }
}