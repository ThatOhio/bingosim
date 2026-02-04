namespace BingoSim.Application.Simulation;

/// <summary>
/// Optional diagnostic logging for simulation debugging. Set EnableDiagnosticLogging = true
/// and LogAction to capture verbose output during a single simulation run.
/// </summary>
public static class SimulationDiagnostics
{
    /// <summary>When true, Log() writes to LogAction.</summary>
    public static bool EnableDiagnosticLogging { get; set; }

    /// <summary>When set, Log() invokes this with each message.</summary>
    public static Action<string>? LogAction { get; set; }

    public static void Log(string message)
    {
        if (EnableDiagnosticLogging && LogAction is { } action)
            action(message);
    }
}
