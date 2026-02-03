namespace BingoSim.Application.Simulation.Schedule;

/// <summary>
/// Snapshot DTO for a player's weekly schedule. Null or empty Sessions = always online.
/// </summary>
public sealed class WeeklyScheduleSnapshotDto
{
    /// <summary>Sessions per week. Null or empty = player is always online (no schedule restrictions).</summary>
    public List<ScheduledSessionSnapshotDto>? Sessions { get; init; }
}
