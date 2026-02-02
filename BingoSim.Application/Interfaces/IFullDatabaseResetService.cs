namespace BingoSim.Application.Interfaces;

/// <summary>
/// Service that deletes all application data from the database. For development/testing only.
/// </summary>
public interface IFullDatabaseResetService
{
    /// <summary>
    /// Deletes all rows from all application tables in FK-safe order. Does not drop the database or migrations.
    /// </summary>
    Task ResetAllDataAsync(CancellationToken cancellationToken = default);
}
