namespace BingoSim.Application.StrategyKeys;

/// <summary>
/// Catalog of supported strategy keys for team configuration. Used by UI dropdown and validators.
/// Strategies control grant allocation and task selection for simulation runs.
/// </summary>
public static class StrategyCatalog
{
    /// <summary>Strategy focused on unlocking the next row as quickly as possible.</summary>
    public const string RowUnlocking = "RowUnlocking";

    private static readonly string[] AllKeys = [RowUnlocking];

    /// <summary>Returns all supported strategy keys for dropdown/validation.</summary>
    public static IReadOnlyList<string> GetSupportedKeys() => AllKeys;

    /// <summary>Returns true if the key is supported.</summary>
    public static bool IsSupported(string key) =>
        !string.IsNullOrWhiteSpace(key) && AllKeys.Contains(key.Trim(), StringComparer.Ordinal);
}
