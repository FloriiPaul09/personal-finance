using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Security;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Accounts;

/// <summary>
/// <c>POST /api/accounts</c>: <c>201</c> with the account, <c>Location</c> and <c>ETag</c>. A name already used by
/// another account of the user, archived ones included, gives <c>409</c> <c>account.name_taken</c>.
/// </summary>
public static class CreateAccount
{
    /// <param name="Currency">ISO 4217 code, any case: stored in upper case.</param>
    /// <param name="InitialBalance">Balance before the first movement, any sign, at most two decimals.</param>
    public sealed record Request(
        [Required, MaxLength(Account.NameMaxLength)] string Name,
        [Required] AccountType? Type,
        [Required, Length(3, 3)] string Currency,
        [Required] decimal? InitialBalance);

    internal sealed class Endpoint : IEndpoint<AccountsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPost("", Handle).WithName(nameof(CreateAccount));
    }

    internal static async Task<Created<AccountResponse>> Handle(
        Request request, AppDbContext db, ICurrentUser currentUser, HttpResponse response, CancellationToken cancellationToken)
    {
        var account = new Account(
            currentUser.RequiredUserId(), request.Name, request.Type!.Value, request.Currency, request.InitialBalance!.Value);

        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        var created = await db.FindAccountResponseAsync(account.Id, cancellationToken)
            ?? throw new InvalidOperationException("The account just created cannot be read.");

        response.SetVersion(created.Version);
        return TypedResults.Created($"{AccountsModule.Path}/{account.Id}", created);
    }
}
