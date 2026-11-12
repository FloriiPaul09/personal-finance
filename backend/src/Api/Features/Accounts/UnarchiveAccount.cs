using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Accounts;

/// <summary><c>POST /api/accounts/{id}/unarchive</c>: the account can receive movements again. Same rules as <see cref="ArchiveAccount"/>.</summary>
public static class UnarchiveAccount
{
    internal sealed class Endpoint : IEndpoint<AccountsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPost("{id:guid}/unarchive", Handle).WithName(nameof(UnarchiveAccount));
    }

    internal static Task<Results<Ok<AccountResponse>, NotFound>> Handle(
        Guid id, AppDbContext db, HttpResponse response, CancellationToken cancellationToken) =>
        ArchiveAccount.SetArchivedAsync(id, archived: false, db, response, cancellationToken);
}
