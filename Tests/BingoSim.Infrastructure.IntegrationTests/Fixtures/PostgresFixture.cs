using Npgsql;
using Testcontainers.PostgreSql;

namespace BingoSim.Infrastructure.IntegrationTests.Fixtures;

/// <summary>
/// Shared PostgreSQL container for integration tests. One container is started for the entire
/// test run instead of per-class, significantly reducing startup time (~10 containers → 1).
/// Each test class must use <see cref="CreateIsolatedDatabaseAsync"/> to get a unique database
/// and avoid "cannot drop the currently open database" errors.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Creates an isolated database for a test class. Drops it if it exists (from postgres connection),
    /// creates it fresh, and returns a connection string. Use a unique name per test class
    /// (e.g. GetType().Name) to avoid cross-test pollution.
    /// </summary>
    public async Task<string> CreateIsolatedDatabaseAsync(string databaseName)
    {
        var baseCs = _container.GetConnectionString();
        var builder = new NpgsqlConnectionStringBuilder(baseCs) { Database = "postgres" };

        await using var conn = new NpgsqlConnection(builder.ToString());
        await conn.OpenAsync();

        // Unique name per call to avoid "terminating connection" races when reusing databases
        var baseName = databaseName.Replace(".", "_");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var fullName = $"{baseName}_{suffix}";
        var safeName = fullName.Length > 63 ? fullName[..63] : fullName;

        await using (var createCmd = new NpgsqlCommand($"""CREATE DATABASE "{safeName}" """, conn))
        {
            await createCmd.ExecuteNonQueryAsync();
        }

        builder.Database = safeName;
        return builder.ToString();
    }
}
