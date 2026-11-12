using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Categories;

/// <summary>A category as every endpoint of the module returns it (docs/backend/api-endpoints.md#categories).</summary>
/// <param name="ParentId">The parent of a subcategory; null for a top-level category.</param>
/// <param name="IsInUse">
/// Whether transactions or budgets use the category: its kind is then fixed and it can only be archived.
/// </param>
/// <param name="HasSubcategories">Whether other categories, archived included, have this one as parent.</param>
/// <param name="Version">Row version, the value to send in <c>If-Match</c> (also in <c>ETag</c> on single reads).</param>
public sealed record CategoryResponse(
    Guid Id,
    string Name,
    CategoryKind Kind,
    Guid? ParentId,
    bool IsArchived,
    bool IsInUse,
    bool HasSubcategories,
    uint Version);

internal static class CategoryQueries
{
    /// <summary>
    /// One query for any number of categories: usage and subcategories are <c>EXISTS</c> subqueries on the indexes
    /// <c>(user_id, category_id, date)</c> of transactions, <c>(user_id, category_id, effective_from)</c> of budgets and
    /// the parent foreign key of categories.
    /// </summary>
    public static IQueryable<CategoryResponse> SelectResponses(this IQueryable<Category> categories, AppDbContext db) =>
        categories.Select(category => new CategoryResponse(
            category.Id,
            category.Name,
            category.Kind,
            category.ParentId,
            category.IsArchived,
            db.Transactions.Any(transaction => transaction.CategoryId == category.Id)
                || db.Budgets.Any(budget => budget.CategoryId == category.Id),
            db.Categories.Any(child => child.ParentId == category.Id),
            EF.Property<uint>(category, RowVersion.PropertyName)));

    public static Task<CategoryResponse?> FindCategoryResponseAsync(this AppDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.Categories.Where(category => category.Id == id).SelectResponses(db).SingleOrDefaultAsync(cancellationToken);

    /// <summary>Whether transactions or budgets of the current user reference the category.</summary>
    public static async Task<bool> IsCategoryInUseAsync(this AppDbContext db, Guid categoryId, CancellationToken cancellationToken) =>
        await db.Transactions.AnyAsync(transaction => transaction.CategoryId == categoryId, cancellationToken)
        || await db.Budgets.AnyAsync(budget => budget.CategoryId == categoryId, cancellationToken);

    /// <summary>Whether any category, archived included, has <paramref name="categoryId"/> as parent.</summary>
    public static Task<bool> HasSubcategoriesAsync(this AppDbContext db, Guid categoryId, CancellationToken cancellationToken) =>
        db.Categories.AnyAsync(category => category.ParentId == categoryId, cancellationToken);

    /// <summary>
    /// Order of the list: expense categories first, then income; top-level categories by name, each followed by its
    /// subcategories by name. A subcategory whose parent is not in the list (filtered out) is placed like a top-level one.
    /// </summary>
    /// <remarks>Done in memory: a user has tens of categories, and the order needs the parent's name.</remarks>
    public static IReadOnlyList<CategoryResponse> InDisplayOrder(IReadOnlyList<CategoryResponse> categories)
    {
        var byId = categories.ToDictionary(category => category.Id);
        var names = StringComparer.InvariantCultureIgnoreCase;

        CategoryResponse HeadOf(CategoryResponse category) =>
            category.ParentId is { } parentId && byId.TryGetValue(parentId, out var parent) ? parent : category;

        return categories
            .OrderBy(category => HeadOf(category).Kind == CategoryKind.Expense ? 0 : 1)
            .ThenBy(category => HeadOf(category).Name, names)
            .ThenBy(category => HeadOf(category).Id)
            .ThenBy(category => HeadOf(category).Id == category.Id ? 0 : 1)
            .ThenBy(category => category.Name, names)
            .ThenBy(category => category.Id)
            .ToList();
    }
}
