using PersonalFinance.Domain.Accounts;

namespace PersonalFinance.Testing;

/// <summary>
/// Test data for one user, saved through the domain and EF Core as that user (ADR-0014,
/// docs/backend/testing.md#test-data). One method per entity, every value optional: a test names only what it checks.
/// </summary>
/// <example>
/// <code>
/// var account = await Database.CreateAccountAsync(userId, name: "Carta", initialBalance: -50m);
/// </code>
/// </example>
public static class TestData
{
    public static async Task<Account> CreateAccountAsync(
        this TestDatabase database,
        Guid userId,
        string? name = null,
        AccountType type = AccountType.Checking,
        string currency = "EUR",
        decimal initialBalance = 0m,
        CancellationToken cancellationToken = default)
    {
        var account = new Account(userId, name ?? UniqueName("Conto"), type, currency, initialBalance);

        await using var db = database.CreateContext(userId);
        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }

    /// <summary>A name no other test data of the user has, within the length limits of every entity.</summary>
    public static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..8]}";
}
