using PersonalFinance.Domain.Budgets;
using PersonalFinance.Domain.Common;
using static PersonalFinance.Domain.Tests.TestData;

namespace PersonalFinance.Domain.Tests.Budgets;

public class BudgetTests
{
    [Fact]
    public void Budget_StartsOnTheFirstDayOfTheMonth()
    {
        var category = ExpenseCategory();

        var budget = new Budget(category, new YearMonth(2026, 11), 300m);

        Assert.Equal(new DateOnly(2026, 11, 1), budget.EffectiveFrom);
        Assert.Equal(category.Id, budget.CategoryId);
        Assert.Equal(category.UserId, budget.UserId);
        Assert.Equal(300m, budget.Amount);
    }

    [Fact]
    public void Budget_WithoutAmount_EndsTheBudget() => Assert.Null(new Budget(ExpenseCategory(), new YearMonth(2026, 11), null).Amount);

    [Fact]
    public void Budget_RejectsIncomeCategories() =>
        Rule.Violated(DomainErrors.BudgetCategoryNotExpense, () => new Budget(IncomeCategory(), new YearMonth(2026, 11), 300m));

    [Theory]
    [InlineData("0", DomainErrors.AmountNotPositive)]
    [InlineData("-1", DomainErrors.AmountNotPositive)]
    [InlineData("1.234", DomainErrors.AmountTooManyDecimals)]
    public void Budget_RejectsInvalidAmounts(string amount, string code) =>
        Rule.Violated(code, () => new Budget(ExpenseCategory(), new YearMonth(2026, 11), decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Budget_RejectsANewLimitOnAnArchivedCategory()
    {
        var category = ExpenseCategory();
        category.Archive([]);

        Rule.Violated(DomainErrors.CategoryArchived, () => new Budget(category, new YearMonth(2026, 11), 100m));
    }

    [Fact]
    public void Budget_CanEndTheBudgetOfAnArchivedCategory()
    {
        var category = ExpenseCategory();
        category.Archive([]);

        Assert.Null(new Budget(category, new YearMonth(2026, 11), null).Amount);
    }

    [Fact]
    public void ChangeAmount_ReplacesTheLimit()
    {
        var budget = new Budget(ExpenseCategory(), new YearMonth(2026, 11), 300m);

        budget.ChangeAmount(350m);

        Assert.Equal(350m, budget.Amount);
    }
}
