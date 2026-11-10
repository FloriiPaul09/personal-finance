using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Common;
using PersonalFinance.Domain.Transactions;
using static PersonalFinance.Domain.Tests.TestData;

namespace PersonalFinance.Domain.Tests.Accounts;

public class AccountTests
{
    [Fact]
    public void Create_SetsOwnerIdAndNormalizedName()
    {
        var account = new Account(UserA, " Contanti ", AccountType.Cash, "eur", 50m);

        Assert.Equal(UserA, account.UserId);
        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal("Contanti", account.Name);
        Assert.Equal("CONTANTI", account.NormalizedName);
        Assert.Equal("EUR", account.Currency);
        Assert.False(account.IsArchived);
    }

    [Fact]
    public void Create_RequiresAnOwner() =>
        Rule.Violated(DomainErrors.UserRequired, () => new Account(Guid.Empty, "Contanti", AccountType.Cash, "EUR", 0m));

    [Fact]
    public void Create_RejectsUnknownTypes() =>
        Rule.Violated(DomainErrors.AccountTypeInvalid, () => new Account(UserA, "Contanti", (AccountType)99, "EUR", 0m));

    [Fact]
    public void Create_AllowsNegativeInitialBalance() => Assert.Equal(-120.50m, AnAccount(initialBalance: -120.50m).InitialBalance);

    [Fact]
    public void Create_RejectsInitialBalanceWithThreeDecimals() =>
        Rule.Violated(DomainErrors.AmountTooManyDecimals, () => AnAccount(initialBalance: 10.005m));

    [Fact]
    public void ChangeCurrency_IsAllowedWithoutMovements()
    {
        var account = AnAccount();

        account.ChangeCurrency("usd", hasMovements: false);

        Assert.Equal("USD", account.Currency);
    }

    [Fact]
    public void ChangeCurrency_IsRejectedWithMovements() =>
        Rule.Violated(DomainErrors.AccountCurrencyLocked, () => AnAccount().ChangeCurrency("USD", hasMovements: true));

    [Fact]
    public void ChangeCurrency_ToTheSameCurrencyIsAlwaysAllowed() => AnAccount().ChangeCurrency("eur", hasMovements: true);

    [Fact]
    public void EnsureActive_RejectsArchivedAccounts()
    {
        var account = AnAccount();
        account.Archive();

        Rule.Violated(DomainErrors.AccountArchived, account.EnsureActive);
    }

    [Fact]
    public void Unarchive_MakesTheAccountActiveAgain()
    {
        var account = AnAccount();
        account.Archive();

        account.Unarchive();

        account.EnsureActive();
    }

    [Fact]
    public void Balance_WithoutMovements_IsTheInitialBalance() => Assert.Equal(100m, AnAccount(initialBalance: 100m).BalanceAt([]));

    [Fact]
    public void Balance_AddsIncomeAndSubtractsExpenses()
    {
        var account = AnAccount(initialBalance: 100m);
        var salary = Transaction.Income(account, IncomeCategory(), Today, 1500m);
        var groceries = Transaction.Expense(account, ExpenseCategory(), Today, 80.25m);

        Assert.Equal(1519.75m, account.BalanceAt([salary, groceries]));
    }

    [Fact]
    public void Balance_CanBeNegative()
    {
        var account = AnAccount(initialBalance: 10m);
        var expense = Transaction.Expense(account, ExpenseCategory(), Today, 30m);

        Assert.Equal(-20m, account.BalanceAt([expense]));
    }

    [Fact]
    public void Balance_UpToADate_IgnoresLaterMovements()
    {
        var account = AnAccount();
        var lastDayOfOctober = Transaction.Expense(account, ExpenseCategory(), new DateOnly(2026, 10, 31), 10m);
        var firstDayOfNovember = Transaction.Expense(account, ExpenseCategory(), new DateOnly(2026, 11, 1), 20m);

        Assert.Equal(-10m, account.BalanceAt([lastDayOfOctober, firstDayOfNovember], new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Balance_RejectsMovementsOfAnotherAccount()
    {
        var account = AnAccount();
        var other = AnAccount(name: "Carta");
        var expense = Transaction.Expense(other, ExpenseCategory(), Today, 10m);

        Rule.Violated(DomainErrors.AccountTransactionMismatch, () => account.BalanceAt([expense]));
    }

    [Fact]
    public void Balance_IncludesTransferLegs()
    {
        var checking = AnAccount(initialBalance: 500m);
        var savings = AnAccount(name: "Risparmi");
        var transfer = new Transfer(checking, savings, Today, 200m);

        Assert.Equal(300m, checking.BalanceAt([transfer.Outgoing]));
        Assert.Equal(200m, savings.BalanceAt([transfer.Incoming]));
    }
}
