using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Common;

namespace PersonalFinance.Domain.Budgets;

/// <summary>
/// One level per branch: a budget is set on a parent category or on its subcategories, never on both, so no expense
/// is counted in two budgets (docs/database/domain-model.md#budget-and-progress). A category counts as budgeted when
/// it has at least one budget entry.
/// </summary>
public static class BudgetPolicy
{
    /// <param name="category">The category that receives a budget entry.</param>
    /// <param name="categories">The user's categories (at least the subcategories of <paramref name="category"/>).</param>
    /// <param name="budgetedCategoryIds">Categories that already have budget entries.</param>
    public static void EnsureCanBudget(Category category, IEnumerable<Category> categories, IReadOnlySet<Guid> budgetedCategoryIds)
    {
        var conflict = category.ParentId is { } parentId
            ? budgetedCategoryIds.Contains(parentId)
            : categories.Any(c => c.ParentId == category.Id && budgetedCategoryIds.Contains(c.Id));

        if (conflict)
        {
            throw new DomainException(
                DomainErrors.BudgetBranchConflict,
                "A budget is set on a category or on its subcategories, not on both.");
        }
    }

    /// <summary>Moving a budgeted category under a budgeted parent would count its expenses twice.</summary>
    public static void EnsureCanMove(Category category, Category? newParent, IReadOnlySet<Guid> budgetedCategoryIds)
    {
        if (newParent is not null && budgetedCategoryIds.Contains(category.Id) && budgetedCategoryIds.Contains(newParent.Id))
        {
            throw new DomainException(
                DomainErrors.BudgetBranchConflict,
                "A budgeted category cannot move under a budgeted parent.");
        }
    }
}
