using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Categories;

/// <summary>
/// <c>DELETE /api/categories/{id}</c>, with <c>If-Match</c>: only a category without transactions, budgets and
/// subcategories. Otherwise <c>409</c> <c>category.in_use</c> or <c>category.has_subcategories</c>: the app offers to
/// archive it instead.
/// </summary>
public static class DeleteCategory
{
    internal sealed class Endpoint : IEndpoint<CategoriesModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapDelete("{id:guid}", Handle).WithName(nameof(DeleteCategory));
    }

    internal static async Task<Results<NoContent, NotFound>> Handle(
        Guid id, IfMatch ifMatch, AppDbContext db, CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(category => category.Id == id, cancellationToken);
        if (category is null)
        {
            return TypedResults.NotFound();
        }

        db.EnsureVersion(category, ifMatch);
        category.EnsureCanDelete(
            await db.HasSubcategoriesAsync(category.Id, cancellationToken),
            await db.IsCategoryInUseAsync(category.Id, cancellationToken));

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
