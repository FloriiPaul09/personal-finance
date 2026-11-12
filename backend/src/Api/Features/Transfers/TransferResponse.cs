using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Transfers;

/// <summary>A transfer as every endpoint of the module returns it (docs/backend/api-endpoints.md#transfers).</summary>
/// <param name="Amount">The amount moved, always positive.</param>
/// <param name="OutgoingTransactionId">The leg on the source account, as listed by <c>/api/transactions</c>.</param>
/// <param name="IncomingTransactionId">The leg on the destination account.</param>
/// <param name="Version">Version of the whole transfer, legs included: the value to send in <c>If-Match</c>.</param>
public sealed record TransferResponse(
    Guid Id,
    Guid FromAccountId,
    Guid ToAccountId,
    DateOnly Date,
    decimal Amount,
    string? Description,
    Guid OutgoingTransactionId,
    Guid IncomingTransactionId,
    uint Version);

internal static class TransferQueries
{
    /// <summary>The transfer of the current user with both legs, tracked; <see langword="null"/> when there is none.</summary>
    public static Task<Transfer?> FindTransferAsync(this AppDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.Transfers.Include(transfer => transfer.Legs).SingleOrDefaultAsync(transfer => transfer.Id == id, cancellationToken);

    /// <summary>The response of a tracked transfer, with the version as loaded or, after saving, as written.</summary>
    public static TransferResponse ToResponse(this AppDbContext db, Transfer transfer) => new(
        transfer.Id,
        transfer.Outgoing.AccountId,
        transfer.Incoming.AccountId,
        transfer.Date,
        transfer.Amount,
        transfer.Description,
        transfer.Outgoing.Id,
        transfer.Incoming.Id,
        RowVersion.Of(db, transfer));

    /// <summary>
    /// The version of a transfer covers its legs: every change rewrites the <c>transfers</c> row too, so a client that
    /// read an older version gets <c>412</c> even though only the legs changed (ADR-0018).
    /// </summary>
    public static void TouchVersion(this AppDbContext db, Transfer transfer) =>
        db.Entry(transfer).Property(item => item.UpdatedAt).IsModified = true;
}
