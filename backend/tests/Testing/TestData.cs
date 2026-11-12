using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Budgets;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Transactions;

namespace PersonalFinance.Testing;

/// <summary>
/// Test data for one user, saved through the domain and EF Core as that user (ADR-0014,
/// docs/backend/testing.md#test-data). One method per entity, every value optional: a test names only what it checks.
/// </summary>
/// <example>
/// <code>
/// var account = await Database.CreateAccountAsync(userId, name: "Carta", initialBalance: -50m);
/// </code>
/// </example>
public static class TestData
{
    /// <summary>Date of movements created without one.</summary>
    public static readonly DateOnly DefaultDate = new(2026, 1, 15);

    public static async Task<Account> CreateAccountAsync(
        this TestDatabase database,
        Guid userId,
        string? name = null,
        AccountType type = AccountType.Checking,
        string currency = "EUR",
        decimal initialBalance = 0m,
        CancellationToken cancellationToken = default)
    {
        var account = new Account(userId, name ?? UniqueName("Conto"), type, currency, initialBalance);
        return await database.SaveAsync(userId, account, cancellationToken);
    }

    public static async Task<Category> CreateCategoryAsync(
        this TestDatabase database,
        Guid userId,
        string? name = null,
        CategoryKind kind = CategoryKind.Expense,
        Category? parent = null,
        CancellationToken cancellationToken = default)
    {
        var category = new Category(userId, name ?? UniqueName("Categoria"), parent?.Kind ?? kind, parent);
        return await database.SaveAsync(userId, category, cancellationToken);
    }

    /// <summary>A budget entry on an expense category, from <paramref name="from"/> (January 2026 by default); a null amount ends the budget.</summary>
    public static async Task<Budget> CreateBudgetAsync(
        this TestDatabase database,
        Category category,
        decimal? amount = 300m,
        YearMonth? from = null,
        CancellationToken cancellationToken = default)
    {
        var budget = new Budget(category, from ?? YearMonth.Of(DefaultDate), amount);
        return await database.SaveAsync(category.UserId, budget, cancellationToken);
    }

    /// <summary>An income on <paramref name="account"/>, in a new income category unless one is given.</summary>
    public static async Task<Transaction> CreateIncomeAsync(
        this TestDatabase database,
        Account account,
        decimal amount = 100m,
        DateOnly? date = null,
        Category? category = null,
        string? description = null,
        IReadOnlyCollection<Tag>? tags = null,
        CancellationToken cancellationToken = default)
    {
        category ??= await database.CreateCategoryAsync(account.UserId, kind: CategoryKind.Income, cancellationToken: cancellationToken);
        var income = Transaction.Income(account, category, date ?? DefaultDate, amount, description);
        income.ReplaceTags(tags ?? []);
        return await database.SaveAsync(account.UserId, income, cancellationToken);
    }

    /// <summary>An expense on <paramref name="account"/>, in a new expense category unless one is given.</summary>
    public static async Task<Transaction> CreateExpenseAsync(
        this TestDatabase database,
        Account account,
        decimal amount = 100m,
        DateOnly? date = null,
        Category? category = null,
        string? description = null,
        IReadOnlyCollection<Tag>? tags = null,
        CancellationToken cancellationToken = default)
    {
        category ??= await database.CreateCategoryAsync(account.UserId, kind: CategoryKind.Expense, cancellationToken: cancellationToken);
        var expense = Transaction.Expense(account, category, date ?? DefaultDate, amount, description);
        expense.ReplaceTags(tags ?? []);
        return await database.SaveAsync(account.UserId, expense, cancellationToken);
    }

    public static async Task<Transfer> CreateTransferAsync(
        this TestDatabase database,
        Account from,
        Account to,
        decimal amount = 100m,
        DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        var transfer = new Transfer(from, to, date ?? DefaultDate, amount);
        return await database.SaveAsync(from.UserId, transfer, cancellationToken);
    }

    public static async Task<Tag> CreateTagAsync(
        this TestDatabase database,
        Guid userId,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        var tag = new Tag(userId, name ?? UniqueName("tag"));
        return await database.SaveAsync(userId, tag, cancellationToken);
    }

    private static async Task<TEntity> SaveAsync<TEntity>(
        this TestDatabase database, Guid userId, TEntity entity, CancellationToken cancellationToken)
        where TEntity : class
    {
        await using var db = database.CreateContext(userId);
        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    /// <summary>A name no other test data of the user has, within the length limits of every entity.</summary>
    public static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..8]}";
}
