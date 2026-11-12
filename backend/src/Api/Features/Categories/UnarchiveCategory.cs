using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Categories;

/// <summary>
/// <c>POST /api/categories/{id}/unarchive</c>: restores this category only; subcategories are restored one by one. A
/// subcategory of an archived parent gives <c>409</c> <c>category.archived</c>. Idempotent, no <c>If-Match</c>.
/// </summary>
public static class UnarchiveCategory
{
    internal sealed class Endpoint : IEndpoint<CategoriesModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPost("{id:guid}/unarchive", Handle).WithName(nameof(UnarchiveCategory));
    }

    internal static async Task<Results<Ok<CategoryResponse>, NotFound>> Handle(
        Guid id, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(category => category.Id == id, cancellationToken);
        if (category is null)
        {
            return TypedResults.NotFound();
        }

        var parentIsArchived = category.ParentId is { } parentId
            && await db.Categories.AnyAsync(parent => parent.Id == parentId && parent.IsArchived, cancellationToken);

        category.Unarchive(parentIsArchived);
        await db.SaveChangesAsync(cancellationToken);

        var result = await db.FindCategoryResponseAsync(category.Id, cancellationToken)
            ?? throw new InvalidOperationException("The category just restored cannot be read.");

        response.SetVersion(result.Version);
        return TypedResults.Ok(result);
    }
}
