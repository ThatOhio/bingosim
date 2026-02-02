namespace BingoSim.Core.Exceptions;

/// <summary>
/// Thrown when a simulation batch is not found.
/// </summary>
public class SimulationBatchNotFoundException : Exception
{
    public Guid BatchId { get; }

    public SimulationBatchNotFoundException(Guid batchId)
        : base($"Simulation batch with id '{batchId}' was not found.")
    {
        BatchId = batchId;
    }
}
