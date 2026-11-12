using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Accounts;

/// <summary>An account as every endpoint of the module returns it (docs/backend/api-endpoints.md#accounts).</summary>
/// <param name="Balance">Initial balance plus every movement, computed by the database at each read (ADR-0010).</param>
/// <param name="HasMovements">
/// Whether any transaction uses the account: the currency is then fixed and the account can only be archived.
/// </param>
/// <param name="Version">Row version, the value to send in <c>If-Match</c> (also in <c>ETag</c> on single reads).</param>
public sealed record AccountResponse(
    Guid Id,
    string Name,
    AccountType Type,
    string Currency,
    decimal InitialBalance,
    decimal Balance,
    bool IsArchived,
    bool HasMovements,
    uint Version);

internal static class AccountQueries
{
    /// <summary>
    /// One query for any number of accounts: the balance and the movement check are correlated subqueries on the
    /// index <c>(user_id, account_id, date)</c> of <c>transactions</c>, filtered by the current user like the accounts.
    /// </summary>
    /// <remarks>
    /// The balance is the same as <c>Account.BalanceAt</c> (compared by <c>AccountsTests.Get_Balance_AddsEveryMovement_AndEqualsTheDomainBalance</c>). When accounts
    /// hold many years of movements, monthly snapshots can replace the full sum here (docs/backend/api-endpoints.md#scalability).
    /// </remarks>
    public static IQueryable<AccountResponse> SelectResponses(this IQueryable<Account> accounts, AppDbContext db) =>
        accounts.Select(account => new AccountResponse(
            account.Id,
            account.Name,
            account.Type,
            account.Currency,
            account.InitialBalance,
            account.InitialBalance
                + (db.Transactions.Where(transaction => transaction.AccountId == account.Id).Sum(transaction => (decimal?)transaction.Amount) ?? 0m),
            account.IsArchived,
            db.Transactions.Any(transaction => transaction.AccountId == account.Id),
            EF.Property<uint>(account, RowVersion.PropertyName)));

    /// <summary>The account <paramref name="id"/> of the current user, or <see langword="null"/>.</summary>
    public static Task<AccountResponse?> FindAccountResponseAsync(this AppDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.Accounts.Where(account => account.Id == id).SelectResponses(db).SingleOrDefaultAsync(cancellationToken);

    /// <summary>Whether any transaction of the current user is on the account.</summary>
    public static Task<bool> HasMovementsAsync(this AppDbContext db, Guid accountId, CancellationToken cancellationToken) =>
        db.Transactions.AnyAsync(transaction => transaction.AccountId == accountId, cancellationToken);
}
