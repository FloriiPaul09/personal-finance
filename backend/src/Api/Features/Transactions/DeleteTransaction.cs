using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Transactions;

/// <summary>
/// <c>DELETE /api/transactions/{id}</c>, with <c>If-Match</c>: deletes an income or an expense for good, with its tag
/// links. A transfer leg is deleted with its transfer (<c>409</c> <c>transaction.is_transfer_leg</c>).
/// </summary>
public static class DeleteTransaction
{
    internal sealed class Endpoint : IEndpoint<TransactionsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapDelete("{id:guid}", Handle).WithName(nameof(DeleteTransaction));
    }

    internal static async Task<Results<NoContent, NotFound>> Handle(
        Guid id, IfMatch ifMatch, AppDbContext db, CancellationToken cancellationToken)
    {
        var transaction = await db.Transactions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (transaction is null)
        {
            return TypedResults.NotFound();
        }

        db.EnsureVersion(transaction, ifMatch);
        transaction.EnsureCanDelete();

        db.Transactions.Remove(transaction);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
