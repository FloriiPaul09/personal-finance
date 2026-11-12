using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Categories;

/// <summary><c>GET /api/categories/{id}</c>: one category, archived or not; <c>ETag</c> with its version.</summary>
public static class GetCategory
{
    internal sealed class Endpoint : IEndpoint<CategoriesModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("{id:guid}", Handle).WithName(nameof(GetCategory));
    }

    internal static async Task<Results<Ok<CategoryResponse>, NotFound>> Handle(
        Guid id, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var category = await db.FindCategoryResponseAsync(id, cancellationToken);
        if (category is null)
        {
            return TypedResults.NotFound();
        }

        response.SetVersion(category.Version);
        return TypedResults.Ok(category);
    }
}
