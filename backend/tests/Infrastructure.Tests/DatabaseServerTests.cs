using Npgsql;
using PersonalFinance.Testing;

namespace PersonalFinance.Infrastructure.Tests;

// Guards the test infrastructure of ADR-0011: pinned image, one database per class, isolated clones.
public sealed class DatabaseServerTests(DatabaseServer server) : DatabaseTest(server)
{
    [Fact]
    public async Task ClassDatabase_RunsThePostgresVersionPinnedInCompose()
    {
        await using var connection = await Database.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var version = await ScalarAsync<string>(connection, "SHOW server_version");

        // postgres:18.6 or postgres:18.6-trixie → 18.6
        var pinned = PostgresImage.Name["postgres:".Length..].Split('-')[0];
        Assert.StartsWith(pinned + " ", version + " ", StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClassDatabase_IsACloneOfTheTemplate()
    {
        await using var connection = await Database.OpenConnectionAsync(TestContext.Current.CancellationToken);

        var current = await ScalarAsync<string>(connection, "SELECT current_database()");

        Assert.Equal(Database.Name, current);
        Assert.NotEqual(PostgresServerFixture.TemplateDatabaseName, current);
    }

    [Fact]
    public async Task ClassDatabase_IsSharedByTheTestsOfTheClass()
    {
        var again = await Server.DatabaseForClassAsync(typeof(DatabaseServerTests));

        Assert.Same(Database, again);
    }

    [Fact]
    public async Task CreatedDatabases_DoNotShareData()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = await Server.CreateDatabaseAsync(cancellationToken);
        var second = await Server.CreateDatabaseAsync(cancellationToken);

        await using (var connection = await first.OpenConnectionAsync(cancellationToken))
        {
            await using var create = new NpgsqlCommand("CREATE TABLE isolation_probe (id int)", connection);
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var other = await second.OpenConnectionAsync(cancellationToken);
        var table = await ScalarAsync<object>(other, "SELECT to_regclass('isolation_probe')");

        Assert.NotEqual(first.Name, second.Name);
        Assert.True(table is null or DBNull);
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return (T)result!;
    }
}
