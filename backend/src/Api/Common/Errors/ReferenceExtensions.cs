using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Common;

namespace PersonalFinance.Api.Common.Errors;

public static class ReferenceExtensions
{
    /// <summary>
    /// The entity an id in the request body points to, among the current user's data (query filter
    /// <c>UserScope</c>). Another user's id and a nonexistent id give the same <c>400</c> with code
    /// <c>{resource}.not_found</c> on <paramref name="field"/> (ADR-0013).
    /// </summary>
    /// <param name="resource">Lowercase name of the resource, e.g. <c>account</c>.</param>
    /// <param name="field">Request field holding the id, e.g. <c>accountId</c>.</param>
    public static async Task<TEntity> GetReferenceAsync<TEntity>(
        this IQueryable<TEntity> source,
        Guid id,
        string resource,
        string field,
        CancellationToken cancellationToken)
        where TEntity : OwnedEntity =>
        await source.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken)
        ?? throw ApiProblemException.ReferenceNotFound(resource, field);
}
