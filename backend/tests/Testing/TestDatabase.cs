using Npgsql;

namespace PersonalFinance.Testing;

/// <summary>A database cloned from the migrated template (ADR-0011).</summary>
public sealed record TestDatabase(string Name, string ConnectionString)
{
    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
