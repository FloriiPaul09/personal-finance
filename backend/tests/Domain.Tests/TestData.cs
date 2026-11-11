using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Common;

namespace PersonalFinance.Domain.Tests;

/// <summary>Valid entities with sensible defaults, so each test states only what matters to it.</summary>
internal static class TestData
{
    public static readonly Guid UserA = Guid.CreateVersion7();
    public static readonly Guid UserB = Guid.CreateVersion7();

    public static readonly DateOnly Today = new(2026, 11, 10);

    public static Account AnAccount(Guid? user = null, string name = "Conto corrente", string currency = "EUR", decimal initialBalance = 0m) =>
        new(user ?? UserA, name, AccountType.Checking, currency, initialBalance);

    public static Category ExpenseCategory(Guid? user = null, string name = "Spesa", Category? parent = null) =>
        new(user ?? UserA, name, CategoryKind.Expense, parent);

    public static Category IncomeCategory(Guid? user = null, string name = "Stipendio", Category? parent = null) =>
        new(user ?? UserA, name, CategoryKind.Income, parent);
}

internal static class Rule
{
    /// <summary>Asserts that <paramref name="action"/> violates the business rule identified by <paramref name="code"/>.</summary>
    public static void Violated(string code, Action action)
    {
        var exception = Assert.Throws<DomainException>(action);
        Assert.Equal(code, exception.Code);
    }
}
