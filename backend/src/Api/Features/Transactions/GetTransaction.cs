using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Transactions;

/// <summary><c>GET /api/transactions/{id}</c>: one transaction, a transfer leg included; <c>ETag</c> with its version.</summary>
public static class GetTransaction
{
    internal sealed class Endpoint : IEndpoint<TransactionsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("{id:guid}", Handle).WithName(nameof(GetTransaction));
    }

    internal static async Task<Results<Ok<TransactionResponse>, NotFound>> Handle(
        Guid id, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var transaction = await db.FindTransactionResponseAsync(id, cancellationToken);
        if (transaction is null)
        {
            return TypedResults.NotFound();
        }

        response.SetVersion(transaction.Version);
        return TypedResults.Ok(transaction);
    }
}
