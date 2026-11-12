using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Requests;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Transfers;

/// <summary>
/// <c>POST /api/transfers</c>: moves money between two accounts of the user, <c>201</c> with <c>Location</c> and
/// <c>ETag</c>. With an <c>id</c> chosen by the client, sending the request again returns the existing transfer with
/// <c>200</c> and moves nothing twice (ADR-0017, ADR-0018).
/// </summary>
public static class CreateTransfer
{
    /// <param name="Id">Optional UUID version 7 chosen by the client; makes the request safe to send again.</param>
    /// <param name="Amount">Greater than zero, at most two decimals.</param>
    public sealed record Request(
        [UuidV7] Guid? Id,
        [Required] Guid? FromAccountId,
        [Required] Guid? ToAccountId,
        [Required] DateOnly? Date,
        [Required] decimal? Amount,
        [MaxLength(Transaction.DescriptionMaxLength)] string? Description);

    internal sealed class Endpoint : IEndpoint<TransfersModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPost("", Handle).WithName(nameof(CreateTransfer));
    }

    internal static async Task<Results<Created<TransferResponse>, Ok<TransferResponse>>> Handle(
        Request request, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        // Sent again with the same id: the first request already moved the money.
        if (request.Id is { } clientId && await db.FindTransferAsync(clientId, cancellationToken) is { } existing)
        {
            return Replayed(db, existing, response);
        }

        var from = await db.Accounts.GetReferenceAsync(request.FromAccountId!.Value, "account", "fromAccountId", cancellationToken);
        var to = await db.Accounts.GetReferenceAsync(request.ToAccountId!.Value, "account", "toAccountId", cancellationToken);

        var transfer = new Transfer(from, to, request.Date!.Value, request.Amount!.Value, request.Description, request.Id);
        db.Transfers.Add(transfer);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (request.Id is not null && IsDuplicateId(exception))
        {
            // The same request, sent twice at the same instant: the other one won.
            db.ChangeTracker.Clear();
            var winner = await db.FindTransferAsync(request.Id.Value, cancellationToken)
                ?? throw new ApiProblemException(
                    StatusCodes.Status409Conflict, ApiErrors.Conflict, "This id is already used: generate a new one.", "id");

            return Replayed(db, winner, response);
        }

        var created = db.ToResponse(transfer);
        response.SetVersion(created.Version);
        return TypedResults.Created($"{TransfersModule.Path}/{transfer.Id}", created);
    }

    private static Ok<TransferResponse> Replayed(AppDbContext db, Transfer transfer, HttpResponse response)
    {
        var body = db.ToResponse(transfer);
        response.SetVersion(body.Version);
        return TypedResults.Ok(body);
    }

    private static bool IsDuplicateId(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_transfers" };
}
