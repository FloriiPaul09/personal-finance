using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Testing;

namespace PersonalFinance.Infrastructure.Tests.Persistence;

// What the domain saves is what it reads back: types, dates, decimals, enums, relationships.
public sealed class RoundTripTests(DatabaseServer server) : PersistenceTest(server)
{
    [Fact]
    public async Task Account_RoundTrips()
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await using var db = Database.CreateContext(data.UserId);

        var account = await db.Accounts.SingleAsync(a => a.Id == data.Checking.Id, Ct);

        Assert.Equal("Conto corrente", account.Name);
        Assert.Equal("CONTO CORRENTE", account.NormalizedName);
        Assert.Equal(AccountType.Checking, account.Type);
        Assert.Equal("EUR", account.Currency);
        Assert.Equal(SampleData.CheckingInitialBalance, account.InitialBalance);
        Assert.Equal(data.UserId, account.UserId);
        Assert.False(account.IsArchived);
    }

    [Fact]
    public async Task Enums_AreStoredAsSnakeCaseText()
    {
        var userId = await NewUserAsync();
        await using (var db = Database.CreateContext(userId))
        {
            db.Accounts.Add(new Account(userId, "Carta", AccountType.CreditCard, "EUR", 0m));
            await db.SaveChangesAsync(Ct);
        }

        var type = await SqlScalarAsync<string>(
            "SELECT type FROM accounts WHERE user_id = @user AND normalized_name = 'CARTA'", ("user", userId));

        Assert.Equal("credit_card", type);
    }

    [Fact]
    public async Task Expense_RoundTripsWithDateAmountAndTags()
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await using var db = Database.CreateContext(data.UserId);

        var expense = await db.Transactions.Include(t => t.Tags).SingleAsync(t => t.Id == data.Expense.Id, Ct);

        Assert.Equal(TransactionKind.Expense, expense.Kind);
        Assert.Equal(new DateOnly(2026, 11, 3), expense.Date);
        Assert.Equal(-42.90m, expense.Amount);
        Assert.Equal("Supermercato", expense.Description);
        Assert.Equal(data.Groceries.Id, expense.CategoryId);
        Assert.Equal(data.Checking.Id, expense.AccountId);
        Assert.Equal(data.Tag.Id, Assert.Single(expense.Tags).TagId);
    }

    [Fact]
    public async Task Transfer_RoundTripsWithBothLegs()
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await using var db = Database.CreateContext(data.UserId);

        var transfer = await db.Transfers.Include(t => t.Legs).SingleAsync(t => t.Id == data.Transfer.Id, Ct);

        Assert.Equal(2, transfer.Legs.Count);
        Assert.Equal(data.Checking.Id, transfer.Outgoing.AccountId);
        Assert.Equal(-200m, transfer.Outgoing.Amount);
        Assert.Equal(data.Savings.Id, transfer.Incoming.AccountId);
        Assert.Equal(200m, transfer.Amount);
        Assert.Equal(new DateOnly(2026, 11, 5), transfer.Date);
        Assert.Equal("Accantonamento", transfer.Description);
    }

    [Fact]
    public async Task Subcategory_And_Budget_RoundTrip()
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        Guid fuelId;
        await using (var write = Database.CreateContext(data.UserId))
        {
            var transport = new Category(data.UserId, "Trasporti", CategoryKind.Expense);
            var fuel = new Category(data.UserId, "Carburante", CategoryKind.Expense, transport);
            write.Categories.AddRange(transport, fuel);
            await write.SaveChangesAsync(Ct);
            fuelId = fuel.Id;
        }

        await using var db = Database.CreateContext(data.UserId);
        var subcategory = await db.Categories.SingleAsync(c => c.Id == fuelId, Ct);
        var budget = await db.Budgets.SingleAsync(b => b.Id == data.Budget.Id, Ct);

        Assert.NotNull(subcategory.ParentId);
        Assert.Equal(CategoryKind.Expense, subcategory.Kind);
        Assert.Equal(new DateOnly(2026, 11, 1), budget.EffectiveFrom);
        Assert.Equal(300m, budget.Amount);
        Assert.Equal(data.Groceries.Id, budget.CategoryId);
    }

    [Fact]
    public async Task LargestAmount_IsKeptExactly()
    {
        var userId = await NewUserAsync();
        var largest = 9_999_999_999_999_999.99m;
        await using (var write = Database.CreateContext(userId))
        {
            write.Accounts.Add(new Account(userId, "Grande", AccountType.Other, "EUR", largest));
            await write.SaveChangesAsync(Ct);
        }

        await using var db = Database.CreateContext(userId);
        Assert.Equal(largest, (await db.Accounts.SingleAsync(a => a.NormalizedName == "GRANDE", Ct)).InitialBalance);
    }

    [Fact]
    public async Task Balance_FromSql_EqualsTheDomainBalance()
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await using var db = Database.CreateContext(data.UserId);

        var account = await db.Accounts.SingleAsync(a => a.Id == data.Checking.Id, Ct);
        var movements = await db.Transactions.Where(t => t.AccountId == account.Id).ToListAsync(Ct);
        var sqlBalance = account.InitialBalance
            + await db.Transactions.Where(t => t.AccountId == account.Id).SumAsync(t => t.Amount, Ct);

        Assert.Equal(SampleData.CheckingBalance, account.BalanceAt(movements));
        Assert.Equal(SampleData.CheckingBalance, sqlBalance);
    }
}
