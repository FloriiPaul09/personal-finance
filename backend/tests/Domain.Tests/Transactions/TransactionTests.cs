using PersonalFinance.Domain.Common;
using PersonalFinance.Domain.Transactions;
using static PersonalFinance.Domain.Tests.TestData;

namespace PersonalFinance.Domain.Tests.Transactions;

public class TransactionTests
{
    [Fact]
    public void Income_IsStoredPositive()
    {
        var account = AnAccount();

        var income = Transaction.Income(account, IncomeCategory(), Today, 1500m, "Stipendio di novembre");

        Assert.Equal(1500m, income.Amount);
        Assert.Equal(TransactionKind.Income, income.Kind);
        Assert.Equal(account.Id, income.AccountId);
        Assert.Equal(account.UserId, income.UserId);
        Assert.Null(income.TransferId);
    }

    [Fact]
    public void Expense_IsStoredNegative()
    {
        var category = ExpenseCategory();

        var expense = Transaction.Expense(AnAccount(), category, Today, 42.90m);

        Assert.Equal(-42.90m, expense.Amount);
        Assert.Equal(category.Id, expense.CategoryId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Expense_RejectsAmountsThatAreNotPositive(int amount) =>
        Rule.Violated(DomainErrors.AmountNotPositive, () => Transaction.Expense(AnAccount(), ExpenseCategory(), Today, amount));

    [Fact]
    public void Expense_RejectsThreeDecimals() =>
        Rule.Violated(DomainErrors.AmountTooManyDecimals, () => Transaction.Expense(AnAccount(), ExpenseCategory(), Today, 1.999m));

    [Fact]
    public void Expense_RejectsAnIncomeCategory() =>
        Rule.Violated(DomainErrors.CategoryKindMismatch, () => Transaction.Expense(AnAccount(), IncomeCategory(), Today, 10m));

    [Fact]
    public void Income_RejectsAnExpenseCategory() =>
        Rule.Violated(DomainErrors.CategoryKindMismatch, () => Transaction.Income(AnAccount(), ExpenseCategory(), Today, 10m));

    [Fact]
    public void Expense_RejectsAnArchivedAccount()
    {
        var account = AnAccount();
        account.Archive();

        Rule.Violated(DomainErrors.AccountArchived, () => Transaction.Expense(account, ExpenseCategory(), Today, 10m));
    }

    [Fact]
    public void Expense_RejectsAnArchivedCategory()
    {
        var category = ExpenseCategory();
        category.Archive([]);

        Rule.Violated(DomainErrors.CategoryArchived, () => Transaction.Expense(AnAccount(), category, Today, 10m));
    }

    [Fact]
    public void Expense_RejectsACategoryOfAnotherUser() =>
        Rule.Violated(DomainErrors.DifferentUser, () => Transaction.Expense(AnAccount(UserA), ExpenseCategory(UserB), Today, 10m));

    [Fact]
    public void Description_IsTrimmedAndEmptyBecomesNull()
    {
        Assert.Equal("Pane", Transaction.Expense(AnAccount(), ExpenseCategory(), Today, 2m, "  Pane ").Description);
        Assert.Null(Transaction.Expense(AnAccount(), ExpenseCategory(), Today, 2m, "   ").Description);
    }

    [Fact]
    public void Change_UpdatesAnExpenseAndKeepsItNegative()
    {
        var expense = Transaction.Expense(AnAccount(), ExpenseCategory(), Today, 10m);
        var otherAccount = AnAccount(name: "Carta");
        var otherCategory = ExpenseCategory(name: "Svago");

        expense.Change(otherAccount, otherCategory, Today.AddDays(-1), 25.50m, "Cinema");

        Assert.Equal(-25.50m, expense.Amount);
        Assert.Equal(otherAccount.Id, expense.AccountId);
        Assert.Equal(otherCategory.Id, expense.CategoryId);
        Assert.Equal(Today.AddDays(-1), expense.Date);
        Assert.Equal("Cinema", expense.Description);
        Assert.Equal(TransactionKind.Expense, expense.Kind);
    }

    [Fact]
    public void Change_OnAnAccountArchivedLater_IsAllowed()
    {
        var account = AnAccount();
        var category = ExpenseCategory();
        var expense = Transaction.Expense(account, category, Today, 10m);
        account.Archive();
        category.Archive([]);

        expense.Change(account, category, Today, 12m, "Corretto");

        Assert.Equal(-12m, expense.Amount);
    }

    [Fact]
    public void Change_ToAnArchivedAccount_IsRejected()
    {
        var expense = Transaction.Expense(AnAccount(), ExpenseCategory(), Today, 10m);
        var archived = AnAccount(name: "Vecchio conto");
        archived.Archive();

        Rule.Violated(DomainErrors.AccountArchived, () => expense.Change(archived, ExpenseCategory(), Today, 10m, null));
    }

    [Fact]
    public void Change_ToACategoryOfTheOtherKind_IsRejected()
    {
        var expense = Transaction.Expense(AnAccount(), ExpenseCategory(), Today, 10m);

        Rule.Violated(DomainErrors.CategoryKindMismatch, () => expense.Change(AnAccount(), IncomeCategory(), Today, 10m, null));
    }

    [Fact]
    public void Change_ToAnAccountOfAnotherUser_IsRejected()
    {
        var expense = Transaction.Expense(AnAccount(), ExpenseCategory(), Today, 10m);

        Rule.Violated(DomainErrors.DifferentUser, () => expense.Change(AnAccount(UserB), ExpenseCategory(UserB), Today, 10m, null));
    }

    [Fact]
    public void Change_OfATransferLeg_IsRejected()
    {
        var transfer = new Transfer(AnAccount(), AnAccount(name: "Risparmi"), Today, 100m);

        Rule.Violated(DomainErrors.TransactionIsTransferLeg, () => transfer.Outgoing.Change(AnAccount(), ExpenseCategory(), Today, 100m, null));
    }

    [Fact]
    public void AddTag_LinksTheTagOnce()
    {
        var expense = Transaction.Expense(AnAccount(), ExpenseCategory(), Today, 10m);
        var holiday = new Tag(UserA, "vacanza-2026");

        expense.AddTag(holiday);
        expense.AddTag(holiday);

        var link = Assert.Single(expense.Tags);
        Assert.Equal(holiday.Id, link.TagId);
        Assert.Equal(expense.Id, link.TransactionId);
        Assert.Equal(UserA, link.UserId);
    }

    [Fact]
    public void AddTag_RejectsATagOfAnotherUser()
    {
        var expense = Transaction.Expense(AnAccount(), ExpenseCategory(), Today, 10m);

        Rule.Violated(DomainErrors.DifferentUser, () => expense.AddTag(new Tag(UserB, "vacanza-2026")));
    }

    [Fact]
    public void RemoveTag_UnlinksTheTag()
    {
        var expense = Transaction.Expense(AnAccount(), ExpenseCategory(), Today, 10m);
        var holiday = new Tag(UserA, "vacanza-2026");
        expense.AddTag(holiday);

        expense.RemoveTag(holiday.Id);

        Assert.Empty(expense.Tags);
    }

    [Fact]
    public void Tag_IsComparedWithoutRegardToCase() =>
        Assert.Equal(new Tag(UserA, "Vacanza-2026").NormalizedName, new Tag(UserA, "vacanza-2026").NormalizedName);
}
