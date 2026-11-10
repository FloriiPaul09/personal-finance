using Xunit;

namespace PersonalFinance.Testing;

/// <summary>
/// Base class for tests that use the database of their test class (ADR-0011).
/// Tests of the same class share the database, never data: every test creates its own users
/// and never asserts on counts across the whole database.
/// </summary>
public abstract class DatabaseTest(PostgresServerFixture server) : IAsyncLifetime
{
    private TestDatabase? _database;

    protected PostgresServerFixture Server { get; } = server;

    protected TestDatabase Database =>
        _database ?? throw new InvalidOperationException("The test database is available once the test has started.");

    public async ValueTask InitializeAsync() => _database = await Server.DatabaseForClassAsync(GetType());

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
