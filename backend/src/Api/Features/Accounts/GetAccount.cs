using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Accounts;

/// <summary><c>GET /api/accounts/{id}</c>: one account with its balance, archived or not; <c>ETag</c> with its version.</summary>
public static class GetAccount
{
    internal sealed class Endpoint : IEndpoint<AccountsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("{id:guid}", Handle).WithName(nameof(GetAccount));
    }

    internal static async Task<Results<Ok<AccountResponse>, NotFound>> Handle(
        Guid id, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var account = await db.FindAccountResponseAsync(id, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        response.SetVersion(account.Version);
        return TypedResults.Ok(account);
    }
}
