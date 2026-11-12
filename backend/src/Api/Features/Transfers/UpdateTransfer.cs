using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Transfers;

/// <summary>
/// <c>PUT /api/transfers/{id}</c>, with <c>If-Match</c>: replaces accounts, date, amount and description; both legs
/// change together. An archived account is refused only when newly chosen.
/// </summary>
public static class UpdateTransfer
{
    public sealed record Request(
        [Required] Guid? FromAccountId,
        [Required] Guid? ToAccountId,
        [Required] DateOnly? Date,
        [Required] decimal? Amount,
        [MaxLength(Transaction.DescriptionMaxLength)] string? Description);

    internal sealed class Endpoint : IEndpoint<TransfersModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPut("{id:guid}", Handle).WithName(nameof(UpdateTransfer));
    }

    internal static async Task<Results<Ok<TransferResponse>, NotFound>> Handle(
        Guid id, Request request, IfMatch ifMatch, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var transfer = await db.FindTransferAsync(id, cancellationToken);
        if (transfer is null)
        {
            return TypedResults.NotFound();
        }

        db.EnsureVersion(transfer, ifMatch);

        var from = await db.Accounts.GetReferenceAsync(request.FromAccountId!.Value, "account", "fromAccountId", cancellationToken);
        var to = await db.Accounts.GetReferenceAsync(request.ToAccountId!.Value, "account", "toAccountId", cancellationToken);

        transfer.Change(from, to, request.Date!.Value, request.Amount!.Value, request.Description);
        db.TouchVersion(transfer);
        await db.SaveChangesAsync(cancellationToken);

        var updated = db.ToResponse(transfer);
        response.SetVersion(updated.Version);
        return TypedResults.Ok(updated);
    }
}
