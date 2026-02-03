namespace BingoSim.Application.Simulation.Snapshot;

/// <summary>
/// Thrown when snapshot validation fails. Message describes the missing or invalid field.
/// </summary>
public sealed class SnapshotValidationException : InvalidOperationException
{
    public SnapshotValidationException(string message) : base(message) { }

    public SnapshotValidationException(string message, Exception inner) : base(message, inner) { }
}
