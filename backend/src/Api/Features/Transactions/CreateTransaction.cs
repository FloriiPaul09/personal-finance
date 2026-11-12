using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Requests;
using PersonalFinance.Api.Common.Security;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Transactions;

/// <summary>
/// <c>POST /api/transactions</c>: an income or an expense, <c>201</c> with <c>Location</c> and <c>ETag</c>. With an
/// <c>id</c> chosen by the client, sending the same request again (e.g. from the offline queue of the PWA after a lost
/// response) returns the existing transaction with <c>200</c> and creates nothing (ADR-0017).
/// </summary>
public static class CreateTransaction
{
    /// <param name="Id">Optional UUID version 7 chosen by the client; makes the request safe to send again.</param>
    /// <param name="Kind"><c>income</c> or <c>expense</c>; transfers are created through <c>/api/transfers</c>.</param>
    /// <param name="Amount">Greater than zero, at most two decimals: the sign follows <see cref="Kind"/>.</param>
    /// <param name="Tags">Tag names; missing tags are created.</param>
    public sealed record Request(
        [UuidV7] Guid? Id,
        [Required] TransactionKind? Kind,
        [Required] Guid? AccountId,
        [Required] Guid? CategoryId,
        [Required] DateOnly? Date,
        [Required] decimal? Amount,
        [MaxLength(Transaction.DescriptionMaxLength)] string? Description,
        [MaxLength(TagNames.MaxPerTransaction)] IReadOnlyList<string>? Tags) : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Kind == TransactionKind.Transfer)
            {
                yield return new ValidationResult("Transfers are created through /api/transfers.", [nameof(Kind)]);
            }
        }
    }

    internal sealed class Endpoint : IEndpoint<TransactionsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPost("", Handle).WithName(nameof(CreateTransaction));
    }

    internal static async Task<Results<Created<TransactionResponse>, Ok<TransactionResponse>>> Handle(
        Request request, AppDbContext db, ICurrentUser currentUser, HttpResponse response, CancellationToken cancellationToken)
    {
        // Sent again with the same id: the first request already created it.
        if (request.Id is { } clientId && await db.FindTransactionResponseAsync(clientId, cancellationToken) is { } existing)
        {
            response.SetVersion(existing.Version);
            return TypedResults.Ok(existing);
        }

        var account = await db.Accounts.GetReferenceAsync(request.AccountId!.Value, "account", "accountId", cancellationToken);
        var category = await db.Categories.GetReferenceAsync(request.CategoryId!.Value, "category", "categoryId", cancellationToken);

        var transaction = request.Kind == TransactionKind.Income
            ? Transaction.Income(account, category, request.Date!.Value, request.Amount!.Value, request.Description, request.Id)
            : Transaction.Expense(account, category, request.Date!.Value, request.Amount!.Value, request.Description, request.Id);

        transaction.ReplaceTags(await TagNames.ResolveAsync(db, currentUser.RequiredUserId(), request.Tags, cancellationToken));
        db.Transactions.Add(transaction);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (request.Id is not null && IsDuplicateId(exception))
        {
            // The same request, sent twice at the same instant: the other one won.
            db.ChangeTracker.Clear();
            var winner = await db.FindTransactionResponseAsync(request.Id.Value, cancellationToken)
                ?? throw new ApiProblemException(
                    StatusCodes.Status409Conflict, ApiErrors.Conflict, "This id is already used: generate a new one.", "id");

            response.SetVersion(winner.Version);
            return TypedResults.Ok(winner);
        }

        var created = await db.FindTransactionResponseAsync(transaction.Id, cancellationToken)
            ?? throw new InvalidOperationException("The transaction just created cannot be read.");

        response.SetVersion(created.Version);
        return TypedResults.Created($"{TransactionsModule.Path}/{transaction.Id}", created);
    }

    private static bool IsDuplicateId(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_transactions" };
}
