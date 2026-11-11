using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Common;
using PersonalFinance.Domain.Transactions;

namespace PersonalFinance.Domain.Budgets;

/// <summary>
/// How much of a category's monthly limit has been spent (docs/database/domain-model.md#budget-and-progress).
/// Values are exact decimals; rounding happens only when displaying.
/// </summary>
public sealed record BudgetProgress(YearMonth Month, decimal Limit, decimal Spent)
{
    /// <summary>Negative when over budget.</summary>
    public decimal Remaining => Limit - Spent;

    /// <summary>Spent / limit; above 1 when over budget.</summary>
    public decimal Ratio => Spent / Limit;

    public bool IsOverBudget => Spent > Limit;

    /// <summary>
    /// The limit of <paramref name="month"/>: the entry with the latest start not after the month; null when there is
    /// no such entry or when it ends the budget.
    /// </summary>
    public static decimal? LimitFor(IEnumerable<Budget> entries, YearMonth month) =>
        entries
            .Where(entry => entry.EffectiveFrom <= month.FirstDay)
            .OrderByDescending(entry => entry.EffectiveFrom)
            .Select(entry => entry.Amount)
            .FirstOrDefault();

    /// <summary>
    /// Progress of <paramref name="category"/> in <paramref name="month"/>, counting the expenses of the category and
    /// of its subcategories; null when the category has no budget that month.
    /// </summary>
    /// <param name="entries">The budget entries of the category.</param>
    /// <param name="categories">The user's categories (at least the subcategories of <paramref name="category"/>).</param>
    /// <param name="transactions">Transactions to consider; only expenses of the branch dated in the month count.</param>
    public static BudgetProgress? Calculate(
        Category category,
        IEnumerable<Budget> entries,
        IEnumerable<Category> categories,
        IEnumerable<Transaction> transactions,
        YearMonth month)
    {
        var categoryEntries = entries.ToList();
        if (categoryEntries.Exists(entry => entry.CategoryId != category.Id))
        {
            throw new DomainException(DomainErrors.BudgetEntriesMismatch, "The budget entries belong to another category.");
        }

        var limit = LimitFor(categoryEntries, month);
        if (limit is null)
        {
            return null;
        }

        var branch = categories
            .Where(c => c.ParentId == category.Id)
            .Select(c => c.Id)
            .Append(category.Id)
            .ToHashSet();

        var spent = transactions
            .Where(t => t.Kind == TransactionKind.Expense
                     && t.CategoryId is { } categoryId && branch.Contains(categoryId)
                     && month.Contains(t.Date))
            .Sum(t => -t.Amount);

        return new BudgetProgress(month, limit.Value, spent);
    }
}
