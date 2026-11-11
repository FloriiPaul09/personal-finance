using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Common;

namespace PersonalFinance.Domain.Budgets;

/// <summary>
/// One entry of the monthly limit of an expense category: it applies from <see cref="EffectiveFrom"/> until the next
/// entry of the same category. A null amount ends the budget from that month (docs/database/domain-model.md#budget-and-progress).
/// </summary>
/// <remarks>Before creating an entry, callers check the one-level-per-branch rule with <see cref="BudgetPolicy"/>.</remarks>
public sealed class Budget : OwnedEntity
{
    public Budget(Category category, YearMonth effectiveFrom, decimal? amount)
        : base(category.UserId)
    {
        if (category.Kind != CategoryKind.Expense)
        {
            throw new DomainException(DomainErrors.BudgetCategoryNotExpense, "Budgets apply to expense categories only.");
        }

        CategoryId = category.Id;
        EffectiveFrom = effectiveFrom.FirstDay;
        ChangeAmount(amount);
    }

    /// <summary>Used by EF Core when materializing entities.</summary>
    private Budget()
    {
    }

    public Guid CategoryId { get; private set; }

    /// <summary>Always the first day of a month.</summary>
    public DateOnly EffectiveFrom { get; private set; }

    /// <summary>Monthly limit, greater than zero; null means no budget from this month.</summary>
    public decimal? Amount { get; private set; }

    public void ChangeAmount(decimal? amount) => Amount = amount is null ? null : Amounts.EnsurePositive(amount.Value);
}
