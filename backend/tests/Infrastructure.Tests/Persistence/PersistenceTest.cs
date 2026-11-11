using Microsoft.EntityFrameworkCore;
using Npgsql;
using PersonalFinance.Testing;

namespace PersonalFinance.Infrastructure.Tests.Persistence;

/// <summary>Helpers shared by persistence tests. Every test creates its own users (ADR-0011).</summary>
public abstract class PersistenceTest(DatabaseServer server) : DatabaseTest(server)
{
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected Task<Guid> NewUserAsync() => Database.CreateUserAsync(Ct);

    /// <summary>Runs SQL directly, bypassing the domain, EF Core and the query filter.</summary>
    protected async Task<int> SqlAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await Database.OpenConnectionAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync(Ct);
    }

    protected async Task<T?> SqlScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await Database.OpenConnectionAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync(Ct);
        return result is null or DBNull ? default : (T)result;
    }

    /// <summary>Asserts that PostgreSQL rejects the SQL with <paramref name="sqlState"/>.</summary>
    protected async Task AssertSqlRejectedAsync(string sqlState, string sql, params (string Name, object Value)[] parameters)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() => SqlAsync(sql, parameters));
        Assert.Equal(sqlState, exception.SqlState);
    }

    /// <summary>Asserts that saving through EF Core fails in PostgreSQL with <paramref name="sqlState"/>.</summary>
    protected static async Task AssertSaveRejectedAsync(string sqlState, DbContext db)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(sqlState, postgres.SqlState);
    }
}

/// <summary>PostgreSQL error codes asserted by the tests.</summary>
internal static class SqlStates
{
    public const string ForeignKeyViolation = "23503";
    public const string UniqueViolation = "23505";
    public const string CheckViolation = "23514";
}
