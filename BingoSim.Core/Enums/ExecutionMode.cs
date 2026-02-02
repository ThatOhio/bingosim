namespace BingoSim.Core.Enums;

/// <summary>
/// Execution mode for a simulation batch (local in-process or distributed workers).
/// </summary>
public enum ExecutionMode
{
    Local = 0,
    Distributed = 1
}
