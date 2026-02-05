namespace BingoSim.Application.Interfaces;

/// <summary>
/// Seeds database with real-world event data (e.g., Bingo7).
/// Each event is built out progressively as event data is provided.
/// </summary>
public interface IRealEventSeedService
{
    /// <summary>
    /// Seeds the specified real event. Event key must match a known event (e.g., "Bingo7").
    /// </summary>
    Task SeedEventAsync(string eventKey, CancellationToken cancellationToken = default);
}
