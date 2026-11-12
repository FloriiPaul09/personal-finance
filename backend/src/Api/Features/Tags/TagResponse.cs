using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Tags;

/// <summary>A tag as every endpoint of the module returns it.</summary>
/// <param name="TransactionCount">How many transactions carry it: the app can sort suggestions by use.</param>
/// <param name="Version">Row version, the value to send in <c>If-Match</c>.</param>
public sealed record TagResponse(Guid Id, string Name, int TransactionCount, uint Version);

internal static class TagQueries
{
    public static IQueryable<TagResponse> SelectResponses(this IQueryable<Tag> tags, AppDbContext db) =>
        tags.Select(tag => new TagResponse(
            tag.Id,
            tag.Name,
            db.TransactionTags.Count(link => link.TagId == tag.Id),
            EF.Property<uint>(tag, RowVersion.PropertyName)));

    public static Task<TagResponse?> FindTagResponseAsync(this AppDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.Tags.Where(tag => tag.Id == id).SelectResponses(db).SingleOrDefaultAsync(cancellationToken);
}
