using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Testing;

namespace PersonalFinance.Infrastructure.Tests.Persistence;

// Owners and timestamps handled by OwnershipInterceptor (docs/database/user-isolation.md#interceptor).
public sealed class InterceptorTests(DatabaseServer server) : PersistenceTest(server)
{
    private static readonly DateTimeOffset Created = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new(2026, 11, 12, 18, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Insert_ForAnotherUser_IsRefused()
    {
        var a = await NewUserAsync();
        var b = await NewUserAsync();
        await using var db = Database.CreateContext(b);

        db.Accounts.Add(new Account(a, "Contanti", AccountType.Cash, "EUR", 0m));

        await Assert.ThrowsAsync<UserIsolationException>(() => db.SaveChangesAsync(Ct));
        await using var owner = Database.CreateContext(a);
        Assert.False(await owner.Accounts.AnyAsync(Ct));
    }

    [Fact]
    public async Task Insert_WithoutCurrentUser_IsRefused()
    {
        var a = await NewUserAsync();
        await using var db = Database.CreateContext(userId: null);

        db.Accounts.Add(new Account(a, "Contanti", AccountType.Cash, "EUR", 0m));

        await Assert.ThrowsAsync<UserIsolationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Owner_CannotBeChanged()
    {
        var a = await NewUserAsync();
        var b = await NewUserAsync();
        await using var db = Database.CreateContext(a);
        var account = new Account(a, "Contanti", AccountType.Cash, "EUR", 0m);
        db.Accounts.Add(account);
        await db.SaveChangesAsync(Ct);

        // UserId has a private setter: only EF metadata can try to change it. EF refuses (part of a key) or the interceptor does.
        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () =>
        {
            db.Entry(account).Property(nameof(Account.UserId)).CurrentValue = b;
            await db.SaveChangesAsync(Ct);
        });
    }

    [Fact]
    public async Task Insert_SetsCreatedAndUpdated()
    {
        var a = await NewUserAsync();
        var account = new Account(a, "Contanti", AccountType.Cash, "EUR", 0m);
        await using (var db = Database.CreateContext(a, new TestClock(Created)))
        {
            db.Accounts.Add(account);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = Database.CreateContext(a);
        var saved = await read.Accounts.SingleAsync(x => x.Id == account.Id, Ct);
        Assert.Equal(Created, saved.CreatedAt);
        Assert.Equal(Created, saved.UpdatedAt);
    }

    [Fact]
    public async Task Update_ChangesOnlyUpdated()
    {
        var a = await NewUserAsync();
        var account = new Account(a, "Contanti", AccountType.Cash, "EUR", 0m);
        await using (var db = Database.CreateContext(a, new TestClock(Created)))
        {
            db.Accounts.Add(account);
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = Database.CreateContext(a, new TestClock(Updated)))
        {
            var loaded = await db.Accounts.SingleAsync(x => x.Id == account.Id, Ct);
            loaded.Rename("Portafoglio");
            await db.SaveChangesAsync(Ct);
        }

        await using var read = Database.CreateContext(a);
        var saved = await read.Accounts.SingleAsync(x => x.Id == account.Id, Ct);
        Assert.Equal(Created, saved.CreatedAt);
        Assert.Equal(Updated, saved.UpdatedAt);
        Assert.Equal("Portafoglio", saved.Name);
    }

    [Fact]
    public async Task NewUser_GetsCreatedAt()
    {
        var a = await NewUserAsync();
        await using var db = Database.CreateContext(userId: null);

        var user = await db.Users.SingleAsync(u => u.Id == a, Ct);

        Assert.Equal(TestClock.Default.GetUtcNow(), user.CreatedAt);
    }

    [Fact]
    public async Task ConcurrentEdits_TheSecondSaveFails()
    {
        var a = await NewUserAsync();
        var account = new Account(a, "Contanti", AccountType.Cash, "EUR", 0m);
        await using (var db = Database.CreateContext(a))
        {
            db.Accounts.Add(account);
            await db.SaveChangesAsync(Ct);
        }

        await using var phone = Database.CreateContext(a);
        await using var computer = Database.CreateContext(a);
        var onPhone = await phone.Accounts.SingleAsync(x => x.Id == account.Id, Ct);
        var onComputer = await computer.Accounts.SingleAsync(x => x.Id == account.Id, Ct);

        onPhone.Rename("Dal telefono");
        await phone.SaveChangesAsync(Ct);

        onComputer.Rename("Dal computer");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => computer.SaveChangesAsync(Ct));
    }
}
