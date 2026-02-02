namespace BingoSim.Core.Entities;

/// <summary>
/// Config snapshot for a simulation batch. Effective event + teams + activities/players at batch start.
/// </summary>
public class EventSnapshot
{
    public Guid Id { get; private set; }
    public Guid SimulationBatchId { get; private set; }
    public string EventConfigJson { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Parameterless constructor for EF Core.</summary>
    private EventSnapshot() { }

    public EventSnapshot(Guid simulationBatchId, string eventConfigJson)
    {
        if (simulationBatchId == default)
            throw new ArgumentException("SimulationBatchId cannot be empty.", nameof(simulationBatchId));

        ArgumentNullException.ThrowIfNull(eventConfigJson);

        Id = Guid.NewGuid();
        SimulationBatchId = simulationBatchId;
        EventConfigJson = eventConfigJson;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
