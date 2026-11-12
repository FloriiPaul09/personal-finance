using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Security;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Transactions;

/// <summary>
/// <c>PUT /api/transactions/{id}</c>, with <c>If-Match</c>: replaces account, category, date, amount, description and
/// tags of an income or an expense. The kind never changes; transfer legs change through their transfer (<c>409</c>
/// <c>transaction.is_transfer_leg</c>).
/// </summary>
public static class UpdateTransaction
{
    /// <param name="Amount">Greater than zero: the sign follows the kind of the transaction.</param>
    /// <param name="Tags">The complete list of tag names; omitted or empty removes every tag.</param>
    public sealed record Request(
        [Required] Guid? AccountId,
        [Required] Guid? CategoryId,
        [Required] DateOnly? Date,
        [Required] decimal? Amount,
        [MaxLength(Transaction.DescriptionMaxLength)] string? Description,
        [MaxLength(TagNames.MaxPerTransaction)] IReadOnlyList<string>? Tags);

    internal sealed class Endpoint : IEndpoint<TransactionsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPut("{id:guid}", Handle).WithName(nameof(UpdateTransaction));
    }

    internal static async Task<Results<Ok<TransactionResponse>, NotFound>> Handle(
        Guid id,
        Request request,
        IfMatch ifMatch,
        AppDbContext db,
        ICurrentUser currentUser,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var transaction = await db.Transactions
            .Include(item => item.Tags)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (transaction is null)
        {
            return TypedResults.NotFound();
        }

        db.EnsureVersion(transaction, ifMatch);

        var account = await db.Accounts.GetReferenceAsync(request.AccountId!.Value, "account", "accountId", cancellationToken);
        var category = await db.Categories.GetReferenceAsync(request.CategoryId!.Value, "category", "categoryId", cancellationToken);

        transaction.Change(account, category, request.Date!.Value, request.Amount!.Value, request.Description);
        transaction.ReplaceTags(await TagNames.ResolveAsync(db, currentUser.RequiredUserId(), request.Tags, cancellationToken));

        // Tags live in another table: without this, a change of tags only would leave the row (and its version)
        // untouched, and a stale client could overwrite the tags without a 412.
        db.Entry(transaction).Property(item => item.Date).IsModified = true;
        await db.SaveChangesAsync(cancellationToken);

        var updated = await db.FindTransactionResponseAsync(transaction.Id, cancellationToken)
            ?? throw new InvalidOperationException("The transaction just updated cannot be read.");

        response.SetVersion(updated.Version);
        return TypedResults.Ok(updated);
    }
}
