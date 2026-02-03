namespace BingoSim.Application.Simulation.Schedule;

/// <summary>
/// Snapshot DTO for one play session. Used for schedule evaluation during simulation.
/// </summary>
public sealed class ScheduledSessionSnapshotDto
{
    /// <summary>Day of week (0=Sunday, 1=Monday, ..., 6=Saturday).</summary>
    public required int DayOfWeek { get; init; }

    /// <summary>Session start time as minutes from midnight (0-1439).</summary>
    public required int StartLocalTimeMinutes { get; init; }

    /// <summary>Session duration in minutes.</summary>
    public required int DurationMinutes { get; init; }
}
