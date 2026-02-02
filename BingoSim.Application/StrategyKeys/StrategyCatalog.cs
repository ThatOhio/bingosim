namespace BingoSim.Application.StrategyKeys;

/// <summary>
/// Catalog of supported strategy keys for team configuration. Used by UI dropdown and validators.
/// </summary>
public static class StrategyCatalog
{
    /// <summary>Baseline strategy.</summary>
    public const string RowRush = "RowRush";

    /// <summary>Alternative strategy.</summary>
    public const string GreedyPoints = "GreedyPoints";

    private static readonly string[] AllKeys = [RowRush, GreedyPoints];

    /// <summary>Returns all supported strategy keys for dropdown/validation.</summary>
    public static IReadOnlyList<string> GetSupportedKeys() => AllKeys;

    /// <summary>Returns true if the key is supported.</summary>
    public static bool IsSupported(string key) =>
        !string.IsNullOrWhiteSpace(key) && AllKeys.Contains(key.Trim(), StringComparer.Ordinal);
}
