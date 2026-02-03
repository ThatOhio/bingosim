namespace BingoSim.Application.Simulation.Snapshot;

/// <summary>
/// Validates EventSnapshotDto. Missing or null required fields cause validation to fail with clear errors.
/// Empty lists are valid (explicit semantics). Call after deserialization or snapshot build.
/// </summary>
public static class SnapshotValidator
{
    /// <summary>
    /// Validates the snapshot. Throws SnapshotValidationException if invalid.
    /// </summary>
    public static void Validate(EventSnapshotDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (string.IsNullOrWhiteSpace(snapshot.EventStartTimeEt))
            throw new SnapshotValidationException("EventStartTimeEt is required and must be a valid ISO8601 string.");

        if (!DateTimeOffset.TryParse(snapshot.EventStartTimeEt, out _))
            throw new SnapshotValidationException($"EventStartTimeEt '{snapshot.EventStartTimeEt}' could not be parsed as DateTimeOffset.");

        if (snapshot.Teams is not { Count: > 0 })
            throw new SnapshotValidationException("Snapshot must have at least one team.");

        foreach (var team in snapshot.Teams)
        {
            if (string.IsNullOrWhiteSpace(team.StrategyKey))
                throw new SnapshotValidationException($"Team '{team.TeamName}' (Id={team.TeamId}) has missing or empty StrategyKey.");

            if (team.Players is null)
                throw new SnapshotValidationException($"Team '{team.TeamName}' has null Players.");

            foreach (var player in team.Players)
            {
                if (player.Schedule is null)
                    throw new SnapshotValidationException($"Player '{player.Name}' (Id={player.PlayerId}) has null Schedule. Use Schedule with Sessions=[] for always-online.");
            }
        }

        if (snapshot.ActivitiesById is null)
            throw new SnapshotValidationException("ActivitiesById is required.");

        foreach (var (activityId, activity) in snapshot.ActivitiesById)
        {
            if (activity.ModeSupport is null)
                throw new SnapshotValidationException($"Activity '{activity.Key}' (Id={activityId}) has null ModeSupport.");

            if (activity.Attempts is not { Count: > 0 })
                throw new SnapshotValidationException($"Activity '{activity.Key}' (Id={activityId}) must have at least one Attempt.");

            foreach (var attempt in activity.Attempts)
            {
                if (attempt.Outcomes is not { Count: > 0 })
                    throw new SnapshotValidationException($"Activity '{activity.Key}' Attempt '{attempt.Key}' has no Outcomes.");

                foreach (var outcome in attempt.Outcomes)
                {
                    if (outcome.Grants is null)
                        throw new SnapshotValidationException($"Activity '{activity.Key}' Attempt '{attempt.Key}' has Outcome with null Grants.");
                }
            }

            if (activity.GroupScalingBands is null)
                throw new SnapshotValidationException($"Activity '{activity.Key}' (Id={activityId}) has null GroupScalingBands. Use empty list for no scaling.");
        }

        if (snapshot.Rows is null)
            throw new SnapshotValidationException("Rows is required.");

        foreach (var row in snapshot.Rows)
        {
            if (row.Tiles is null)
                throw new SnapshotValidationException($"Row {row.Index} has null Tiles.");

            foreach (var tile in row.Tiles)
            {
                if (tile.AllowedActivities is null)
                    throw new SnapshotValidationException($"Tile '{tile.Key}' has null AllowedActivities.");

                if (tile.AllowedActivities.Count == 0)
                    throw new SnapshotValidationException($"Tile '{tile.Key}' has no AllowedActivities.");

                foreach (var rule in tile.AllowedActivities)
                {
                    if (rule.Modifiers is null)
                        throw new SnapshotValidationException($"Tile '{tile.Key}' TileActivityRule has null Modifiers. Use empty list for no modifiers.");

                    if (rule.AcceptedDropKeys is null)
                        throw new SnapshotValidationException($"Tile '{tile.Key}' TileActivityRule has null AcceptedDropKeys.");

                    if (rule.RequirementKeys is null)
                        throw new SnapshotValidationException($"Tile '{tile.Key}' TileActivityRule has null RequirementKeys.");

                    if (!snapshot.ActivitiesById.ContainsKey(rule.ActivityDefinitionId))
                        throw new SnapshotValidationException($"Tile '{tile.Key}' references ActivityDefinitionId {rule.ActivityDefinitionId} which is not in ActivitiesById.");
                }
            }
        }
    }
}
