using BingoSim.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace BingoSim.Infrastructure.Services;

/// <summary>
/// Bridges SimulationPersistenceOptions to ISimulationPersistenceConfig.
/// </summary>
public sealed class SimulationPersistenceConfig(IOptions<SimulationPersistenceOptions> options) : ISimulationPersistenceConfig
{
    private readonly SimulationPersistenceOptions _options = options.Value;

    public int BatchSize => _options.BatchSize;
    public int FlushIntervalMs => _options.FlushIntervalMs;
}
