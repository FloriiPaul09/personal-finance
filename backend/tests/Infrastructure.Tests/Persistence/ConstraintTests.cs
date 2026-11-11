using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Budgets;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Testing;

namespace PersonalFinance.Infrastructure.Tests.Persistence;

// The database enforces the rules of docs/database/conventions.md#constraints even when the domain is bypassed.
public sealed class ConstraintTests(DatabaseServer server) : PersistenceTest(server)
{
    private const string InsertTransaction =
        "INSERT INTO transactions (id, user_id, account_id, kind, date, amount, category_id, transfer_id, created_at, updated_at) " +
        "VALUES (@id, @user, @account, @kind, DATE '2026-11-10', @amount, @category, @transfer, now(), now())";

    [Theory]
    [InlineData("eur")]
    [InlineData("EU")]
    [InlineData("E1R")]
    public async Task Account_RejectsInvalidCurrency(string currency)
    {
        var userId = await NewUserAsync();

        await AssertSqlRejectedAsync(
            SqlStates.CheckViolation,
            "INSERT INTO accounts (id, user_id, name, normalized_name, type, currency, initial_balance, is_archived, created_at, updated_at) " +
            "VALUES (@id, @user, 'Conto', 'CONTO', 'checking', @currency, 0, false, now(), now())",
            ("id", Guid.CreateVersion7()), ("user", userId), ("currency", currency));
    }

    [Fact]
    public async Task Account_RejectsUnknownType()
    {
        var userId = await NewUserAsync();

        await AssertSqlRejectedAsync(
            SqlStates.CheckViolation,
            "INSERT INTO accounts (id, user_id, name, normalized_name, type, currency, initial_balance, is_archived, created_at, updated_at) " +
            "VALUES (@id, @user, 'Conto', 'CONTO', 'bank', 'EUR', 0, false, now(), now())",
            ("id", Guid.CreateVersion7()), ("user", userId));
    }

    [Fact]
    public async Task Account_NamesAreUniquePerUser_WithoutRegardToCase()
    {
        var userId = await NewUserAsync();
        var otherUserId = await NewUserAsync();
        await using (var other = Database.CreateContext(otherUserId))
        {
            other.Accounts.Add(new Account(otherUserId, "Contanti", AccountType.Cash, "EUR", 0m));
            await other.SaveChangesAsync(Ct);
        }

        await using var db = Database.CreateContext(userId);
        db.Accounts.Add(new Account(userId, "Contanti", AccountType.Cash, "EUR", 0m));
        await db.SaveChangesAsync(Ct); // same name as another user: allowed

        db.Accounts.Add(new Account(userId, "CONTANTI", AccountType.Cash, "EUR", 0m));
        await AssertSaveRejectedAsync(SqlStates.UniqueViolation, db);
    }

    [Fact]
    public async Task Category_TopLevelNamesAreUniquePerKind()
    {
        var userId = await NewUserAsync();
        await using var db = Database.CreateContext(userId);
        db.Categories.AddRange(
            new Category(userId, "Altro", CategoryKind.Expense),
            new Category(userId, "Altro", CategoryKind.Income));
        await db.SaveChangesAsync(Ct); // same name, different kind: allowed

        db.Categories.Add(new Category(userId, "altro", CategoryKind.Expense));
        await AssertSaveRejectedAsync(SqlStates.UniqueViolation, db);
    }

    [Fact]
    public async Task Category_SubcategoryNamesAreUniquePerParent()
    {
        var userId = await NewUserAsync();
        await using var db = Database.CreateContext(userId);
        var transport = new Category(userId, "Trasporti", CategoryKind.Expense);
        var home = new Category(userId, "Casa", CategoryKind.Expense);
        db.Categories.AddRange(transport, home,
            new Category(userId, "Manutenzione", CategoryKind.Expense, transport),
            new Category(userId, "Manutenzione", CategoryKind.Expense, home));
        await db.SaveChangesAsync(Ct); // same name under different parents: allowed

        db.Categories.Add(new Category(userId, "Manutenzione", CategoryKind.Expense, home));
        await AssertSaveRejectedAsync(SqlStates.UniqueViolation, db);
    }

    [Fact]
    public async Task Tag_NamesAreUniquePerUser()
    {
        var userId = await NewUserAsync();
        await using var db = Database.CreateContext(userId);
        db.Tags.Add(new Tag(userId, "vacanza-2026"));
        await db.SaveChangesAsync(Ct);

        db.Tags.Add(new Tag(userId, "Vacanza-2026"));
        await AssertSaveRejectedAsync(SqlStates.UniqueViolation, db);
    }

    [Fact]
    public async Task Budget_HasOneEntryPerCategoryAndMonth()
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await using var db = Database.CreateContext(data.UserId);
        var groceries = await db.Categories.SingleAsync(c => c.Id == data.Groceries.Id, Ct);

        db.Budgets.Add(new Budget(groceries, new YearMonth(2026, 11), 350m));

        await AssertSaveRejectedAsync(SqlStates.UniqueViolation, db);
    }

    [Theory]
    [InlineData("2026-11-15", 100)]
    [InlineData("2026-11-01", 0)]
    [InlineData("2026-11-01", -5)]
    public async Task Budget_RejectsDaysOtherThanTheFirst_AndAmountsNotPositive(string effectiveFrom, int amount)
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);

        await AssertSqlRejectedAsync(
            SqlStates.CheckViolation,
            "INSERT INTO budgets (id, user_id, category_id, effective_from, amount, created_at, updated_at) " +
            "VALUES (@id, @user, @category, CAST(@from AS date), @amount, now(), now())",
            ("id", Guid.CreateVersion7()), ("user", data.UserId), ("category", data.Groceries.Id),
            ("from", effectiveFrom), ("amount", (decimal)amount));
    }

    [Theory]
    [InlineData("expense", 0)]
    [InlineData("expense", 10)]
    [InlineData("income", -10)]
    public async Task Transaction_SignMustMatchTheKind(string kind, int amount)
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        var category = kind == "income" ? data.Salary.Id : data.Groceries.Id;

        await AssertSqlRejectedAsync(
            SqlStates.CheckViolation,
            InsertTransaction,
            ("id", Guid.CreateVersion7()), ("user", data.UserId), ("account", data.Checking.Id), ("kind", kind),
            ("amount", (decimal)amount), ("category", category), ("transfer", DBNull.Value));
    }

    [Fact]
    public async Task Transaction_ExpenseNeedsACategory()
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);

        await AssertSqlRejectedAsync(
            SqlStates.CheckViolation,
            InsertTransaction,
            ("id", Guid.CreateVersion7()), ("user", data.UserId), ("account", data.Checking.Id), ("kind", "expense"),
            ("amount", -10m), ("category", DBNull.Value), ("transfer", DBNull.Value));
    }

    [Fact]
    public async Task Transaction_TransferLegNeedsATransfer_AndNoCategory()
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);

        await AssertSqlRejectedAsync(
            SqlStates.CheckViolation,
            InsertTransaction,
            ("id", Guid.CreateVersion7()), ("user", data.UserId), ("account", data.Checking.Id), ("kind", "transfer"),
            ("amount", -10m), ("category", DBNull.Value), ("transfer", DBNull.Value));

        await AssertSqlRejectedAsync(
            SqlStates.CheckViolation,
            InsertTransaction,
            ("id", Guid.CreateVersion7()), ("user", data.UserId), ("account", data.Checking.Id), ("kind", "transfer"),
            ("amount", -10m), ("category", data.Groceries.Id), ("transfer", data.Transfer.Id));
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(10)]
    public async Task Transfer_HasAtMostOneLegPerDirection(int amount)
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);

        await AssertSqlRejectedAsync(
            SqlStates.UniqueViolation,
            InsertTransaction,
            ("id", Guid.CreateVersion7()), ("user", data.UserId), ("account", data.Checking.Id), ("kind", "transfer"),
            ("amount", (decimal)amount), ("category", DBNull.Value), ("transfer", data.Transfer.Id));
    }

    [Fact]
    public async Task Account_WithMovements_CannotBeDeleted()
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await using var db = Database.CreateContext(data.UserId);

        db.Accounts.Remove(await db.Accounts.SingleAsync(a => a.Id == data.Checking.Id, Ct));

        await AssertSaveRejectedAsync(SqlStates.ForeignKeyViolation, db);
    }

    [Fact]
    public async Task Category_InUse_CannotBeDeleted()
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await using var db = Database.CreateContext(data.UserId);

        db.Categories.Remove(await db.Categories.SingleAsync(c => c.Id == data.Salary.Id, Ct));

        await AssertSaveRejectedAsync(SqlStates.ForeignKeyViolation, db);
    }

    [Fact]
    public async Task Transfer_IsDeletedWithItsLegs()
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await using var db = Database.CreateContext(data.UserId);

        await db.Transfers.Where(t => t.Id == data.Transfer.Id).ExecuteDeleteAsync(Ct);

        Assert.False(await db.Transactions.AnyAsync(t => t.TransferId == data.Transfer.Id, Ct));
        Assert.True(await db.Transactions.AnyAsync(t => t.Id == data.Expense.Id, Ct));
    }

    [Fact]
    public async Task Tag_IsRemovedFromTransactionsWhenDeleted()
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        await using var db = Database.CreateContext(data.UserId);

        await db.Tags.Where(t => t.Id == data.Tag.Id).ExecuteDeleteAsync(Ct);

        Assert.False(await db.TransactionTags.AnyAsync(Ct));
        Assert.True(await db.Transactions.AnyAsync(t => t.Id == data.Expense.Id, Ct));
    }

    [Fact]
    public async Task User_IsDeletedWithAllTheirData()
    {
        var data = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);
        var other = await SampleData.CreateAsync(Database, await NewUserAsync(), Ct);

        await SqlAsync("DELETE FROM users WHERE id = @user", ("user", data.UserId));

        await using var db = Database.CreateContext(data.UserId);
        Assert.False(await db.Accounts.AnyAsync(Ct));
        Assert.False(await db.Categories.AnyAsync(Ct));
        Assert.False(await db.Transactions.AnyAsync(Ct));
        Assert.False(await db.Transfers.AnyAsync(Ct));
        Assert.False(await db.Tags.AnyAsync(Ct));
        Assert.False(await db.TransactionTags.AnyAsync(Ct));
        Assert.False(await db.Budgets.AnyAsync(Ct));

        await using var otherDb = Database.CreateContext(other.UserId);
        Assert.Equal(4, await otherDb.Transactions.CountAsync(Ct));
    }
}
