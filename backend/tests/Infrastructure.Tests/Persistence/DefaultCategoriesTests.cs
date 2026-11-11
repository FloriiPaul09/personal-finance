using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Testing;

namespace PersonalFinance.Infrastructure.Tests.Persistence;

// Default categories created for a new user (docs/database/default-data.md#tests).
public sealed class DefaultCategoriesTests(DatabaseServer server) : PersistenceTest(server)
{
    [Fact]
    public async Task NewUser_GetsExactlyTheDefaultList()
    {
        var userId = await NewUserAsync();

        var created = await Initializer().EnsureForUserAsync(userId, Ct);

        await using var db = Database.CreateContext(userId);
        var saved = await db.Categories.ToListAsync(Ct);
        Assert.True(created);
        Assert.Equal(
            DefaultCategories.CreateFor(userId).Select(c => (c.Name, c.Kind, c.IsSubcategory)).Order(),
            saved.Select(c => (c.Name, c.Kind, c.IsSubcategory)).Order());

        var home = saved.Single(c => c.Name == "Casa");
        Assert.Equal(3, saved.Count(c => c.ParentId == home.Id));
    }

    [Fact]
    public async Task SecondRun_AddsNothing()
    {
        var userId = await NewUserAsync();
        await Initializer().EnsureForUserAsync(userId, Ct);

        var created = await Initializer().EnsureForUserAsync(userId, Ct);

        await using var db = Database.CreateContext(userId);
        Assert.False(created);
        Assert.Equal(DefaultCategories.CreateFor(userId).Count, await db.Categories.CountAsync(Ct));
    }

    [Fact]
    public async Task UserWithOwnCategories_GetsNoDefaults()
    {
        var userId = await NewUserAsync();
        await using (var write = Database.CreateContext(userId))
        {
            write.Categories.Add(new Category(userId, "Mia categoria", CategoryKind.Expense));
            await write.SaveChangesAsync(Ct);
        }

        var created = await Initializer().EnsureForUserAsync(userId, Ct);

        await using var db = Database.CreateContext(userId);
        Assert.False(created);
        Assert.Equal("Mia categoria", (await db.Categories.SingleAsync(Ct)).Name);
    }

    [Fact]
    public async Task TwoUsers_GetSeparateCopies_InvisibleToEachOther()
    {
        var a = await NewUserAsync();
        var b = await NewUserAsync();

        await Initializer().EnsureForUserAsync(a, Ct);
        await Initializer().EnsureForUserAsync(b, Ct);

        await using var dbA = Database.CreateContext(a);
        await using var dbB = Database.CreateContext(b);
        var idsA = await dbA.Categories.Select(c => c.Id).ToListAsync(Ct);
        var idsB = await dbB.Categories.Select(c => c.Id).ToListAsync(Ct);
        Assert.Equal(idsA.Count, idsB.Count);
        Assert.Empty(idsA.Intersect(idsB));
    }

    private DefaultCategoriesInitializer Initializer() => new(Database.CreateOptions());
}
