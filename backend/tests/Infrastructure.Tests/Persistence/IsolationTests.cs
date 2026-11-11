using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Testing;

namespace PersonalFinance.Infrastructure.Tests.Persistence;

// User B never sees, changes or references the data of user A (docs/database/user-isolation.md#tests).
public sealed class IsolationTests(DatabaseServer server) : PersistenceTest(server)
{
    private const string InsertTransaction =
        "INSERT INTO transactions (id, user_id, account_id, kind, date, amount, category_id, transfer_id, created_at, updated_at) " +
        "VALUES (@id, @user, @account, @kind, DATE '2026-11-10', @amount, @category, @transfer, now(), now())";

    [Fact]
    public async Task Owner_SeesAllTheirData()
    {
        var a = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await using var db = Database.CreateContext(a.UserId);

        Assert.Equal(2, await db.Accounts.CountAsync(Ct));
        Assert.Equal(2, await db.Categories.CountAsync(Ct));
        Assert.Equal(4, await db.Transactions.CountAsync(Ct));
        Assert.Equal(1, await db.Transfers.CountAsync(Ct));
        Assert.Equal(1, await db.Tags.CountAsync(Ct));
        Assert.Equal(1, await db.TransactionTags.CountAsync(Ct));
        Assert.Equal(1, await db.Budgets.CountAsync(Ct));
    }

    [Fact]
    public async Task OtherUser_ReadsNoneOfTheData()
    {
        var a = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await using var db = Database.CreateContext(await NewUserAsync());

        Assert.False(await db.Accounts.AnyAsync(Ct));
        Assert.False(await db.Categories.AnyAsync(Ct));
        Assert.False(await db.Transactions.AnyAsync(Ct));
        Assert.False(await db.Transfers.AnyAsync(Ct));
        Assert.False(await db.Tags.AnyAsync(Ct));
        Assert.False(await db.TransactionTags.AnyAsync(Ct));
        Assert.False(await db.Budgets.AnyAsync(Ct));

        Assert.Null(await db.Accounts.FindAsync([a.Checking.Id], Ct));
        Assert.Null(await db.Transactions.FirstOrDefaultAsync(t => t.Id == a.Expense.Id, Ct));
        Assert.Equal(0m, await db.Transactions.Where(t => t.AccountId == a.Checking.Id).SumAsync(t => t.Amount, Ct));
    }

    [Fact]
    public async Task OtherUser_DoesNotLoadForeignRowsThroughNavigations()
    {
        var a = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        var b = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await using var db = Database.CreateContext(b.UserId);

        var transfers = await db.Transfers.Include(t => t.Legs).ToListAsync(Ct);
        var transactions = await db.Transactions.Include(t => t.Tags).ToListAsync(Ct);

        Assert.Equal(b.Transfer.Id, Assert.Single(transfers).Id);
        Assert.All(transfers.SelectMany(t => t.Legs), leg => Assert.Equal(b.UserId, leg.UserId));
        Assert.All(transactions, t => Assert.Equal(b.UserId, t.UserId));
        Assert.All(transactions.SelectMany(t => t.Tags), link => Assert.NotEqual(a.Tag.Id, link.TagId));
    }

    [Fact]
    public async Task OtherUser_CannotUpdateOrDeleteWithBulkOperations()
    {
        var a = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await using (var db = Database.CreateContext(await NewUserAsync()))
        {
            Assert.Equal(0, await db.Transactions.Where(t => t.Id == a.Expense.Id).ExecuteDeleteAsync(Ct));
            Assert.Equal(0, await db.Accounts.Where(x => x.Id == a.Checking.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsArchived, true), Ct));
        }

        await using var owner = Database.CreateContext(a.UserId);
        Assert.True(await owner.Transactions.AnyAsync(t => t.Id == a.Expense.Id, Ct));
        Assert.False((await owner.Accounts.SingleAsync(x => x.Id == a.Checking.Id, Ct)).IsArchived);
    }

    [Fact]
    public async Task NobodyLoggedIn_ReadsNothing()
    {
        await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await using var db = Database.CreateContext(userId: null);

        Assert.False(await db.Accounts.AnyAsync(Ct));
        Assert.False(await db.Transactions.AnyAsync(Ct));
        Assert.False(await db.Budgets.AnyAsync(Ct));
    }

    [Fact]
    public async Task Database_RejectsATransactionOnAnotherUsersAccount()
    {
        var a = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        var b = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);

        await AssertSqlRejectedAsync(
            SqlStates.ForeignKeyViolation,
            InsertTransaction,
            ("id", Guid.CreateVersion7()), ("user", b.UserId), ("account", a.Checking.Id), ("kind", "expense"),
            ("amount", -10m), ("category", b.Groceries.Id), ("transfer", DBNull.Value));
    }

    [Fact]
    public async Task Database_RejectsATransactionWithAnotherUsersCategory()
    {
        var a = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        var b = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);

        await AssertSqlRejectedAsync(
            SqlStates.ForeignKeyViolation,
            InsertTransaction,
            ("id", Guid.CreateVersion7()), ("user", b.UserId), ("account", b.Checking.Id), ("kind", "expense"),
            ("amount", -10m), ("category", a.Groceries.Id), ("transfer", DBNull.Value));
    }

    [Fact]
    public async Task Database_RejectsALegOfAnotherUsersTransfer()
    {
        var a = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        var b = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await SqlAsync("DELETE FROM transactions WHERE transfer_id = @transfer AND amount > 0", ("transfer", a.Transfer.Id));

        await AssertSqlRejectedAsync(
            SqlStates.ForeignKeyViolation,
            InsertTransaction,
            ("id", Guid.CreateVersion7()), ("user", b.UserId), ("account", b.Savings.Id), ("kind", "transfer"),
            ("amount", 200m), ("category", DBNull.Value), ("transfer", a.Transfer.Id));
    }

    [Fact]
    public async Task Database_RejectsASubcategoryOfAnotherUsersCategory()
    {
        var a = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        var bUserId = await NewUserAsync();

        await AssertSqlRejectedAsync(
            SqlStates.ForeignKeyViolation,
            "INSERT INTO categories (id, user_id, parent_id, kind, name, normalized_name, is_archived, created_at, updated_at) " +
            "VALUES (@id, @user, @parent, 'expense', 'Frutta', 'FRUTTA', false, now(), now())",
            ("id", Guid.CreateVersion7()), ("user", bUserId), ("parent", a.Groceries.Id));
    }

    [Fact]
    public async Task Database_RejectsABudgetOnAnotherUsersCategory()
    {
        var a = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        var bUserId = await NewUserAsync();

        await AssertSqlRejectedAsync(
            SqlStates.ForeignKeyViolation,
            "INSERT INTO budgets (id, user_id, category_id, effective_from, amount, created_at, updated_at) " +
            "VALUES (@id, @user, @category, DATE '2026-12-01', 100, now(), now())",
            ("id", Guid.CreateVersion7()), ("user", bUserId), ("category", a.Groceries.Id));
    }

    [Fact]
    public async Task Database_RejectsATagLinkAcrossUsers()
    {
        var a = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        var b = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);

        await AssertSqlRejectedAsync(
            SqlStates.ForeignKeyViolation,
            "INSERT INTO transaction_tags (transaction_id, tag_id, user_id) VALUES (@transaction, @tag, @user)",
            ("transaction", b.Income.Id), ("tag", a.Tag.Id), ("user", b.UserId));
    }

    [Fact]
    public async Task OtherUser_CannotSaveChangesToForeignRows()
    {
        var a = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        Account account;
        await using (var owner = Database.CreateContext(a.UserId))
        {
            account = await owner.Accounts.AsNoTracking().SingleAsync(x => x.Id == a.Checking.Id, Ct);
        }

        await using var db = Database.CreateContext(await NewUserAsync());
        db.Accounts.Attach(account);
        account.Rename("Rubato");

        await Assert.ThrowsAsync<UserIsolationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task OtherUser_CannotDeleteForeignRows()
    {
        var a = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        Account account;
        await using (var owner = Database.CreateContext(a.UserId))
        {
            account = await owner.Accounts.AsNoTracking().SingleAsync(x => x.Id == a.Savings.Id, Ct);
        }

        await using var db = Database.CreateContext(await NewUserAsync());
        db.Accounts.Remove(account);

        await Assert.ThrowsAsync<UserIsolationException>(() => db.SaveChangesAsync(Ct));
    }
}
