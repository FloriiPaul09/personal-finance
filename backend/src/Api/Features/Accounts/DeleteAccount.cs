using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Accounts;

/// <summary>
/// <c>DELETE /api/accounts/{id}</c>, with <c>If-Match</c>: only an account without movements, e.g. one created by
/// mistake. With movements <c>409</c> <c>account.has_movements</c>: the app offers to archive it instead.
/// </summary>
public static class DeleteAccount
{
    internal sealed class Endpoint : IEndpoint<AccountsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapDelete("{id:guid}", Handle).WithName(nameof(DeleteAccount));
    }

    internal static async Task<Results<NoContent, NotFound>> Handle(
        Guid id, IfMatch ifMatch, AppDbContext db, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(account => account.Id == id, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        db.EnsureVersion(account, ifMatch);
        account.EnsureCanDelete(await db.HasMovementsAsync(account.Id, cancellationToken));

        db.Accounts.Remove(account);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
