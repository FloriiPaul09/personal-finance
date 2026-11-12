using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Requests;
using PersonalFinance.Api.Common.Responses;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Categories;

/// <summary>
/// <c>GET /api/categories</c>: a flat list, each top-level category followed by its subcategories. Filters:
/// <c>?kind=expense|income</c>; <c>?includeArchived=true</c> adds archived categories in their place. Not paged.
/// </summary>
public static class ListCategories
{
    internal sealed class Endpoint : IEndpoint<CategoriesModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("", Handle).WithName(nameof(ListCategories));
    }

    internal static async Task<Ok<ListResponse<CategoryResponse>>> Handle(
        AppDbContext db, CancellationToken cancellationToken, string? kind = null, bool includeArchived = false)
    {
        var kindFilter = QueryValues.ParseEnum<CategoryKind>(kind, nameof(kind));

        var categories = db.Categories.AsQueryable();
        if (kindFilter is { } selectedKind)
        {
            categories = categories.Where(category => category.Kind == selectedKind);
        }

        if (!includeArchived)
        {
            categories = categories.Where(category => !category.IsArchived);
        }

        var items = await categories.SelectResponses(db).ToListAsync(cancellationToken);
        return TypedResults.Ok(new ListResponse<CategoryResponse>(CategoryQueries.InDisplayOrder(items)));
    }
}
