using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Accounts;

/// <summary>
/// <c>POST /api/accounts/{id}/archive</c>: the account keeps its history and balance but receives no new movements.
/// Idempotent and without <c>If-Match</c>: it changes one flag and never overwrites what another device edited
/// (ADR-0015). The response has the new version.
/// </summary>
public static class ArchiveAccount
{
    internal sealed class Endpoint : IEndpoint<AccountsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPost("{id:guid}/archive", Handle).WithName(nameof(ArchiveAccount));
    }

    internal static Task<Results<Ok<AccountResponse>, NotFound>> Handle(
        Guid id, AppDbContext db, HttpResponse response, CancellationToken cancellationToken) =>
        SetArchivedAsync(id, archived: true, db, response, cancellationToken);

    /// <summary>Shared with <see cref="UnarchiveAccount"/>: saves only when the flag changes.</summary>
    internal static async Task<Results<Ok<AccountResponse>, NotFound>> SetArchivedAsync(
        Guid id, bool archived, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(account => account.Id == id, cancellationToken);
        if (account is null)
        {
            return TypedResults.NotFound();
        }

        if (account.IsArchived != archived)
        {
            if (archived)
            {
                account.Archive();
            }
            else
            {
                account.Unarchive();
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        var result = await db.FindAccountResponseAsync(account.Id, cancellationToken)
            ?? throw new InvalidOperationException("The account just changed cannot be read.");

        response.SetVersion(result.Version);
        return TypedResults.Ok(result);
    }
}
