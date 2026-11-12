using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Transfers;

/// <summary>
/// <c>DELETE /api/transfers/{id}</c>, with <c>If-Match</c>: deletes the transfer and both legs for good (foreign key
/// with <c>ON DELETE CASCADE</c>); both balances go back as before.
/// </summary>
public static class DeleteTransfer
{
    internal sealed class Endpoint : IEndpoint<TransfersModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapDelete("{id:guid}", Handle).WithName(nameof(DeleteTransfer));
    }

    internal static async Task<Results<NoContent, NotFound>> Handle(
        Guid id, IfMatch ifMatch, AppDbContext db, CancellationToken cancellationToken)
    {
        var transfer = await db.Transfers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (transfer is null)
        {
            return TypedResults.NotFound();
        }

        db.EnsureVersion(transfer, ifMatch);
        db.Transfers.Remove(transfer);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
