using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Responses;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Accounts;

/// <summary>
/// <c>GET /api/accounts</c>: the active accounts, by name; archived ones too with <c>?includeArchived=true</c>, after
/// the active ones. Not paged: a person has a handful of accounts.
/// </summary>
public static class ListAccounts
{
    internal sealed class Endpoint : IEndpoint<AccountsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("", Handle).WithName(nameof(ListAccounts));
    }

    internal static async Task<Ok<ListResponse<AccountResponse>>> Handle(
        AppDbContext db, CancellationToken cancellationToken, bool includeArchived = false)
    {
        var accounts = includeArchived ? db.Accounts : db.Accounts.Where(account => !account.IsArchived);

        var items = await accounts
            .OrderBy(account => account.IsArchived)
            .ThenBy(account => account.NormalizedName)
            .ThenBy(account => account.Id)
            .SelectResponses(db)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new ListResponse<AccountResponse>(items));
    }
}
