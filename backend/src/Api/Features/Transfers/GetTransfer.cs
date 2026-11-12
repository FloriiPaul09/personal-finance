using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Transfers;

/// <summary>
/// <c>GET /api/transfers/{id}</c>: one transfer with both legs; <c>ETag</c> with its version. There is no list: the
/// legs are listed by <c>GET /api/transactions?kind=transfer</c>, whose items carry <c>transferId</c> (ADR-0018).
/// </summary>
public static class GetTransfer
{
    internal sealed class Endpoint : IEndpoint<TransfersModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("{id:guid}", Handle).WithName(nameof(GetTransfer));
    }

    internal static async Task<Results<Ok<TransferResponse>, NotFound>> Handle(
        Guid id, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var transfer = await db.FindTransferAsync(id, cancellationToken);
        if (transfer is null)
        {
            return TypedResults.NotFound();
        }

        var body = db.ToResponse(transfer);
        response.SetVersion(body.Version);
        return TypedResults.Ok(body);
    }
}
