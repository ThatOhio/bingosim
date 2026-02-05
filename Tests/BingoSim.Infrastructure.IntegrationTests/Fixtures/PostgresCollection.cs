namespace BingoSim.Infrastructure.IntegrationTests.Fixtures;

/// <summary>
/// Collection definition for tests that share the PostgresFixture. All tests in this collection
/// run sequentially and share a single PostgreSQL container, avoiding per-class container startup.
/// </summary>
[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}
