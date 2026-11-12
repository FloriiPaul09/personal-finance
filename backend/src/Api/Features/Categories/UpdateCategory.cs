using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Domain.Budgets;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Categories;

/// <summary>
/// <c>PUT /api/categories/{id}</c>: replaces name, kind and parent together, with <c>If-Match</c>, so renaming, moving
/// and changing the kind follow the same rules (<c>Category.Change</c>, <c>BudgetPolicy.EnsureCanMove</c>).
/// </summary>
public static class UpdateCategory
{
    /// <param name="Kind">Can change only while the category is unused and has no subcategories.</param>
    /// <param name="ParentId">The new parent, or null to make it a top-level category.</param>
    public sealed record Request(
        [Required, MaxLength(Category.NameMaxLength)] string Name,
        [Required] CategoryKind? Kind,
        Guid? ParentId);

    internal sealed class Endpoint : IEndpoint<CategoriesModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPut("{id:guid}", Handle).WithName(nameof(UpdateCategory));
    }

    internal static async Task<Results<Ok<CategoryResponse>, NotFound>> Handle(
        Guid id, Request request, IfMatch ifMatch, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(category => category.Id == id, cancellationToken);
        if (category is null)
        {
            return TypedResults.NotFound();
        }

        db.EnsureVersion(category, ifMatch);

        var parent = request.ParentId is { } parentId
            ? await db.Categories.GetReferenceAsync(parentId, "category", "parentId", cancellationToken)
            : null;

        if (parent?.Id != category.ParentId)
        {
            // A budgeted category under a budgeted parent would count its expenses twice.
            var newParentId = parent?.Id;
            var budgeted = await db.Budgets
                .Where(budget => budget.CategoryId == category.Id || budget.CategoryId == newParentId)
                .Select(budget => budget.CategoryId)
                .Distinct()
                .ToListAsync(cancellationToken);

            BudgetPolicy.EnsureCanMove(category, parent, budgeted.ToHashSet());
        }

        category.Change(
            request.Name,
            request.Kind!.Value,
            parent,
            await db.HasSubcategoriesAsync(category.Id, cancellationToken),
            await db.IsCategoryInUseAsync(category.Id, cancellationToken));

        await db.SaveChangesAsync(cancellationToken);

        var updated = await db.FindCategoryResponseAsync(category.Id, cancellationToken)
            ?? throw new InvalidOperationException("The category just updated cannot be read.");

        response.SetVersion(updated.Version);
        return TypedResults.Ok(updated);
    }
}
