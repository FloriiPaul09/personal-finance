using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Tags;

/// <summary>
/// <c>DELETE /api/tags/{id}</c>, with <c>If-Match</c>: removes the tag from every transaction and deletes it. The
/// transactions themselves do not change.
/// </summary>
public static class DeleteTag
{
    internal sealed class Endpoint : IEndpoint<TagsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapDelete("{id:guid}", Handle).WithName(nameof(DeleteTag));
    }

    internal static async Task<Results<NoContent, NotFound>> Handle(
        Guid id, IfMatch ifMatch, AppDbContext db, CancellationToken cancellationToken)
    {
        var tag = await db.Tags.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (tag is null)
        {
            return TypedResults.NotFound();
        }

        db.EnsureVersion(tag, ifMatch);

        // The links go with it (foreign key with ON DELETE CASCADE).
        db.Tags.Remove(tag);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
