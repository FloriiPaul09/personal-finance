using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Budgets;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Common;
using PersonalFinance.Domain.Transactions;
using static PersonalFinance.Domain.Tests.TestData;

namespace PersonalFinance.Domain.Tests.Budgets;

public class BudgetProgressTests
{
    private static readonly YearMonth January = new(2026, 1);
    private static readonly YearMonth June = new(2026, 6);
    private static readonly YearMonth September = new(2026, 9);

    private readonly Account _account = AnAccount();
    private readonly Category _transport = ExpenseCategory(name: "Trasporti");

    [Fact]
    public void Limit_IsTheLatestEntryNotAfterTheMonth()
    {
        Budget[] entries = [new(_transport, January, 300m), new(_transport, June, 350m)];

        Assert.Equal(300m, BudgetProgress.LimitFor(entries, January));
        Assert.Equal(300m, BudgetProgress.LimitFor(entries, new YearMonth(2026, 5)));
        Assert.Equal(350m, BudgetProgress.LimitFor(entries, June));
        Assert.Equal(350m, BudgetProgress.LimitFor(entries, new YearMonth(2027, 3)));
    }

    [Fact]
    public void Limit_BeforeTheFirstEntry_IsNull() =>
        Assert.Null(BudgetProgress.LimitFor([new Budget(_transport, June, 350m)], January));

    [Fact]
    public void Limit_AfterAnEndingEntry_IsNull_UntilANewEntry()
    {
        Budget[] entries = [new(_transport, January, 300m), new(_transport, June, null), new(_transport, September, 200m)];

        Assert.Null(BudgetProgress.LimitFor(entries, new YearMonth(2026, 7)));
        Assert.Equal(200m, BudgetProgress.LimitFor(entries, September));
    }

    [Fact]
    public void Limit_DoesNotDependOnTheOrderOfEntries()
    {
        Budget[] entries = [new(_transport, June, 350m), new(_transport, January, 300m)];

        Assert.Equal(350m, BudgetProgress.LimitFor(entries, September));
    }

    [Fact]
    public void Spent_CountsExpensesOfTheCategoryAndItsSubcategories()
    {
        var fuel = ExpenseCategory(name: "Carburante", parent: _transport);
        var train = Expense(_transport, new DateOnly(2026, 6, 3), 40m);
        var refuel = Expense(fuel, new DateOnly(2026, 6, 20), 60.50m);

        var progress = Calculate([train, refuel], [_transport, fuel], June);

        Assert.NotNull(progress);
        Assert.Equal(100.50m, progress.Spent);
        Assert.Equal(350m, progress.Limit);
        Assert.Equal(249.50m, progress.Remaining);
        Assert.False(progress.IsOverBudget);
    }

    [Fact]
    public void Spent_IncludesTheFirstAndLastDayOfTheMonth_Only()
    {
        Transaction[] transactions =
        [
            Expense(_transport, new DateOnly(2026, 5, 31), 1m),
            Expense(_transport, new DateOnly(2026, 6, 1), 10m),
            Expense(_transport, new DateOnly(2026, 6, 30), 20m),
            Expense(_transport, new DateOnly(2026, 7, 1), 100m),
        ];

        Assert.Equal(30m, Calculate(transactions, [_transport], June)!.Spent);
    }

    [Fact]
    public void Spent_InFebruaryOfALeapYear_IncludesThe29th()
    {
        var february = new YearMonth(2028, 2);
        Budget[] entries = [new(_transport, february, 100m)];
        var expense = Expense(_transport, new DateOnly(2028, 2, 29), 15m);

        Assert.Equal(15m, BudgetProgress.Calculate(_transport, entries, [_transport], [expense], february)!.Spent);
    }

    [Fact]
    public void Spent_IgnoresOtherCategoriesIncomeAndTransfers()
    {
        var leisure = ExpenseCategory(name: "Svago");
        Transaction[] transactions =
        [
            Expense(leisure, new DateOnly(2026, 6, 5), 80m),
            Transaction.Income(_account, IncomeCategory(), new DateOnly(2026, 6, 5), 2000m),
            .. new Transfer(_account, AnAccount(name: "Risparmi"), new DateOnly(2026, 6, 5), 500m).Legs,
        ];

        Assert.Equal(0m, Calculate(transactions, [_transport, leisure], June)!.Spent);
    }

    [Fact]
    public void Progress_OverBudget_HasNegativeRemainingAndRatioAboveOne()
    {
        var progress = Calculate([Expense(_transport, new DateOnly(2026, 6, 10), 420m)], [_transport], June)!;

        Assert.True(progress.IsOverBudget);
        Assert.Equal(-70m, progress.Remaining);
        Assert.Equal(1.2m, progress.Ratio);
    }

    [Fact]
    public void Progress_WithoutBudget_IsNull() => Assert.Null(Calculate([], [_transport], new YearMonth(2026, 2)));

    [Fact]
    public void Progress_RejectsEntriesOfAnotherCategory()
    {
        Budget[] entries = [new(ExpenseCategory(name: "Svago"), June, 100m)];

        Rule.Violated(DomainErrors.BudgetEntriesMismatch, () => BudgetProgress.Calculate(_transport, entries, [_transport], [], June));
    }

    // Transport budget: 300 € from January, 350 € from June.
    private BudgetProgress? Calculate(IEnumerable<Transaction> transactions, IEnumerable<Category> categories, YearMonth month) =>
        BudgetProgress.Calculate(
            _transport,
            [new Budget(_transport, January, 300m), new Budget(_transport, June, 350m)],
            categories,
            transactions,
            month);

    private Transaction Expense(Category category, DateOnly date, decimal amount) => Transaction.Expense(_account, category, date, amount);
}
