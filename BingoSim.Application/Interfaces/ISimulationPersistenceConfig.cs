namespace BingoSim.Application.Interfaces;

/// <summary>
/// Configuration for batched persistence. Abstraction to avoid Application depending on Infrastructure.
/// </summary>
public interface ISimulationPersistenceConfig
{
    /// <summary>Flush when buffer reaches this many runs. 1 = immediate persist (no batching).</summary>
    int BatchSize { get; }

    /// <summary>Flush when this many ms elapsed since last flush. 0 = count-based only.</summary>
    int FlushIntervalMs { get; }
}
