using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Categories;

/// <summary>
/// <c>POST /api/categories/{id}/archive</c>: archives the category and, for a top-level one, its subcategories. Archived
/// categories keep their transactions and budgets but cannot be chosen for new ones. Idempotent, no <c>If-Match</c>
/// (ADR-0015).
/// </summary>
public static class ArchiveCategory
{
    internal sealed class Endpoint : IEndpoint<CategoriesModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPost("{id:guid}/archive", Handle).WithName(nameof(ArchiveCategory));
    }

    internal static async Task<Results<Ok<CategoryResponse>, NotFound>> Handle(
        Guid id, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(category => category.Id == id, cancellationToken);
        if (category is null)
        {
            return TypedResults.NotFound();
        }

        var subcategories = category.IsSubcategory
            ? new List<Category>()
            : await db.Categories.Where(subcategory => subcategory.ParentId == category.Id).ToListAsync(cancellationToken);

        category.Archive(subcategories);

        // Saves nothing when everything was already archived: the version stays the same.
        await db.SaveChangesAsync(cancellationToken);

        var result = await db.FindCategoryResponseAsync(category.Id, cancellationToken)
            ?? throw new InvalidOperationException("The category just archived cannot be read.");

        response.SetVersion(result.Version);
        return TypedResults.Ok(result);
    }
}
