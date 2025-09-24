namespace BingoSim.Models;

public class ProgressSource
{
    // Optional label for readability
    public string Name { get; set; } = string.Empty;

    // How many independent rolls happen for this source in a single attempt of the activity
    public int RollsPerAttempt { get; set; } = 1;

    // Probability of success per roll (0..1)
    public double ChancePerRoll { get; set; } = 0.0;

    // How many units of progress are granted on success (e.g., 20 coconut milk)
    public int QuantityPerSuccess { get; set; } = 1;
}