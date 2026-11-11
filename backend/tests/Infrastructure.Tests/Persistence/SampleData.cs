using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Budgets;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Testing;

namespace PersonalFinance.Infrastructure.Tests.Persistence;

/// <summary>One of each entity for a user, saved through the domain and EF Core.</summary>
internal sealed record SampleData(
    Guid UserId,
    Account Checking,
    Account Savings,
    Category Groceries,
    Category Salary,
    Transaction Expense,
    Transaction Income,
    Transfer Transfer,
    Tag Tag,
    Budget Budget)
{
    public const decimal CheckingInitialBalance = 100m;

    /// <summary>100 + 1500 − 42.90 − 200.</summary>
    public const decimal CheckingBalance = 1357.10m;

    public static async Task<SampleData> CreateAsync(TestDatabase database, Guid userId, CancellationToken cancellationToken)
    {
        var checking = new Account(userId, "Conto corrente", AccountType.Checking, "EUR", CheckingInitialBalance);
        var savings = new Account(userId, "Risparmi", AccountType.Savings, "EUR", 0m);
        var groceries = new Category(userId, "Spesa", CategoryKind.Expense);
        var salary = new Category(userId, "Stipendio", CategoryKind.Income);
        var tag = new Tag(userId, "vacanza-2026");

        var expense = Transaction.Expense(checking, groceries, new DateOnly(2026, 11, 3), 42.90m, "Supermercato");
        expense.AddTag(tag);
        var income = Transaction.Income(checking, salary, new DateOnly(2026, 11, 1), 1500m, "Stipendio di novembre");
        var transfer = new Transfer(checking, savings, new DateOnly(2026, 11, 5), 200m, "Accantonamento");
        var budget = new Budget(groceries, new YearMonth(2026, 11), 300m);

        await using var db = database.CreateContext(userId);
        db.AddRange(checking, savings, groceries, salary, tag, expense, income, transfer, budget);
        await db.SaveChangesAsync(cancellationToken);

        return new SampleData(userId, checking, savings, groceries, salary, expense, income, transfer, tag, budget);
    }
}
