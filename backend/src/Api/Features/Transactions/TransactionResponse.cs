using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Transactions;

/// <summary>A transaction as every endpoint of the module returns it (docs/backend/api-endpoints.md#transactions).</summary>
/// <param name="CategoryId">Set for income and expenses; null for transfer legs.</param>
/// <param name="TransferId">Set for transfer legs only: the transfer to open to change or delete them.</param>
/// <param name="Amount">Always positive, as the client sends it; <see cref="Kind"/> tells income from expense.</param>
/// <param name="SignedAmount">
/// The effect on the account balance: positive for income and incoming legs, negative for expenses and outgoing legs.
/// </param>
/// <param name="Tags">Tag names, by name.</param>
/// <param name="Version">Row version, the value to send in <c>If-Match</c> (also in <c>ETag</c> on single reads).</param>
public sealed record TransactionResponse(
    Guid Id,
    TransactionKind Kind,
    Guid AccountId,
    Guid? CategoryId,
    Guid? TransferId,
    DateOnly Date,
    decimal Amount,
    decimal SignedAmount,
    string? Description,
    IReadOnlyList<string> Tags,
    uint Version);

internal static class TransactionQueries
{
    public static IQueryable<TransactionResponse> SelectResponses(this IQueryable<Transaction> transactions, AppDbContext db) =>
        transactions.Select(transaction => new TransactionResponse(
            transaction.Id,
            transaction.Kind,
            transaction.AccountId,
            transaction.CategoryId,
            transaction.TransferId,
            transaction.Date,
            transaction.Amount < 0 ? -transaction.Amount : transaction.Amount,
            transaction.Amount,
            transaction.Description,
            db.Tags
                .Where(tag => transaction.Tags.Any(link => link.TagId == tag.Id))
                .OrderBy(tag => tag.NormalizedName)
                .Select(tag => tag.Name)
                .ToList(),
            EF.Property<uint>(transaction, RowVersion.PropertyName)));

    public static Task<TransactionResponse?> FindTransactionResponseAsync(this AppDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.Transactions.Where(transaction => transaction.Id == id).SelectResponses(db).SingleOrDefaultAsync(cancellationToken);
}
