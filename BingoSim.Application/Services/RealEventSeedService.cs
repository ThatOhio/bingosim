using BingoSim.Application.Interfaces;
using BingoSim.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BingoSim.Application.Services;

/// <summary>
/// Seeds real-world event data. Each event (e.g., Bingo7) is built out progressively
/// as event data is provided. Do not fill in gaps—ask for missing information.
/// </summary>
public class RealEventSeedService(
    IPlayerProfileRepository _playerRepo,
    IActivityDefinitionRepository _activityRepo,
    IEventRepository _eventRepo,
    ITeamRepository _teamRepo,
    ILogger<RealEventSeedService> logger) : IRealEventSeedService
{
    public async Task SeedEventAsync(string eventKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
            throw new ArgumentException("Event key cannot be null or empty.", nameof(eventKey));

        var normalized = eventKey.Trim();

        switch (normalized)
        {
            case "Bingo7":
                await SeedBingo7Async(cancellationToken);
                break;
            default:
                throw new ArgumentException($"Unknown real event: '{normalized}'. Supported: Bingo7.", nameof(eventKey));
        }
    }

    /// <summary>
    /// Bingo7 real event. Build out as event data is provided—do not fill gaps.
    /// </summary>
    private async Task SeedBingo7Async(CancellationToken cancellationToken)
    {
        logger.LogInformation("Real event seed: Bingo7 — placeholder (no data yet). Provide event data to build out.");
        await Task.CompletedTask;
    }
}
