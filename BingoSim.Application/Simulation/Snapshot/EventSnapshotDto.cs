namespace BingoSim.Application.Simulation.Snapshot;

/// <summary>
/// Root snapshot DTO for simulation. Serialized to JSON and stored in EventSnapshot.EventConfigJson.
/// </summary>
public sealed class EventSnapshotDto
{
    public required string EventName { get; init; }
    public required int DurationSeconds { get; init; }
    public required int UnlockPointsRequiredPerRow { get; init; }
    public required List<RowSnapshotDto> Rows { get; init; }
    public required Dictionary<Guid, ActivitySnapshotDto> ActivitiesById { get; init; }
    public required List<TeamSnapshotDto> Teams { get; init; }
}
