using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Tags;

/// <summary>
/// <c>PUT /api/tags/{id}</c>, with <c>If-Match</c>: renames a tag on every transaction that carries it, e.g. to fix a
/// typo. A name another tag already has gives <c>409</c> <c>tag.name_taken</c>.
/// </summary>
public static class RenameTag
{
    public sealed record Request([Required, MaxLength(Tag.NameMaxLength)] string Name);

    internal sealed class Endpoint : IEndpoint<TagsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPut("{id:guid}", Handle).WithName(nameof(RenameTag));
    }

    internal static async Task<Results<Ok<TagResponse>, NotFound>> Handle(
        Guid id, Request request, IfMatch ifMatch, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var tag = await db.Tags.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (tag is null)
        {
            return TypedResults.NotFound();
        }

        db.EnsureVersion(tag, ifMatch);
        tag.Rename(request.Name);
        await db.SaveChangesAsync(cancellationToken);

        var renamed = await db.FindTagResponseAsync(tag.Id, cancellationToken)
            ?? throw new InvalidOperationException("The tag just renamed cannot be read.");

        response.SetVersion(renamed.Version);
        return TypedResults.Ok(renamed);
    }
}
