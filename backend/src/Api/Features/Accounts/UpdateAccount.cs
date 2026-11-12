using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Accounts;

/// <summary>
/// <c>PUT /api/accounts/{id}</c>: replaces name, type, currency and initial balance, with <c>If-Match</c>. The currency
/// of an account with movements cannot change (<c>409</c> <c>account.currency_locked</c>). Archived accounts can be
/// edited too: archiving only stops new movements.
/// </summary>
public static class UpdateAccount
{
    /// <param name="Currency">ISO 4217 code, any case. Must stay the same once the account has movements.</param>
    public sealed record Request(
        [Required, MaxLength(Account.NameMaxLength)] string Name,
        [Required] AccountType? Type,
        [Required, Length(3, 3)] string Currency,
        [Required] decimal? InitialBalance);

    internal sealed class Endpoint : IEndpoint<AccountsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPut("{id:guid}", Handle).WithName(nameof(UpdateAccount));
    }

    internal static async Task<Results<Ok<AccountResponse>, NotFound>> Handle(
        Guid id, Request request, IfMatch ifMatch, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(account => account.Id == id, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        db.EnsureVersion(account, ifMatch);

        account.Rename(request.Name);
        account.ChangeType(request.Type!.Value);
        account.ChangeInitialBalance(request.InitialBalance!.Value);
        account.ChangeCurrency(request.Currency, await db.HasMovementsAsync(account.Id, cancellationToken));

        await db.SaveChangesAsync(cancellationToken);

        var updated = await db.FindAccountResponseAsync(account.Id, cancellationToken)
            ?? throw new InvalidOperationException("The account just updated cannot be read.");

        response.SetVersion(updated.Version);
        return TypedResults.Ok(updated);
    }
}
