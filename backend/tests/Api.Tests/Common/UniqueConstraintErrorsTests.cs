using Npgsql;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Tests.Support;

namespace PersonalFinance.Api.Tests.Common;

// Every unique index of the user data has an error code, or is listed as an internal invariant
// (docs/backend/validation-and-errors.md#duplicates). Read from PostgreSQL, so a new migration cannot be missed.
public sealed class UniqueConstraintErrorsTests(ApiServer server) : ApiTest(server)
{
    // Identity manages its own uniqueness (user names, roles) and reports it through IdentityResult.
    private static readonly string[] IdentityTables =
        ["users", "roles", "user_roles", "user_claims", "user_logins", "user_tokens", "role_claims"];

    [Fact]
    public async Task EveryUniqueIndexOfUserData_IsMappedOrInternal()
    {
        var indexes = await UniqueIndexesAsync();

        Assert.NotEmpty(indexes);
        Assert.Equal(
            UniqueConstraintErrors.MappedIndexes.Concat(UniqueConstraintErrors.Internal).Order(StringComparer.Ordinal),
            indexes.Order(StringComparer.Ordinal));
    }

    private async Task<List<string>> UniqueIndexesAsync()
    {
        await using var connection = await Database.OpenConnectionAsync(Ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT indexname
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexdef LIKE 'CREATE UNIQUE INDEX%'
              AND indexname LIKE 'ix\_%'
              AND NOT (tablename::text = ANY(@identity_tables))
            """,
            connection);
        command.Parameters.AddWithValue("identity_tables", IdentityTables);

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
