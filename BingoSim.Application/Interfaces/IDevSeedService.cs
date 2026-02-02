namespace BingoSim.Application.Interfaces;

/// <summary>
/// Service for development seed data. Idempotent seeding and reset of seed-tagged entities.
/// </summary>
public interface IDevSeedService
{
    /// <summary>
    /// Applies dev seed data for Slices 1–3 (Players, Activities, Events).
    /// Idempotent: find-or-create by stable keys; updates existing to match current seed definitions.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes only seed-tagged entities (by stable keys), then re-applies seed.
    /// Does not drop the database.
    /// </summary>
    Task ResetAndSeedAsync(CancellationToken cancellationToken = default);
}
