using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Common;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Testing;

namespace PersonalFinance.Infrastructure.Tests.Persistence;

// The model and the migrations agree, and no table can forget isolation (docs/database/user-isolation.md#tests).
public sealed class ModelTests(DatabaseServer server) : PersistenceTest(server)
{
    [Fact]
    public async Task Migrations_AreAllAppliedToTheTemplate()
    {
        await using var db = Database.CreateContext(userId: null);

        var applied = await db.Database.GetAppliedMigrationsAsync(Ct);

        Assert.NotEmpty(db.Database.GetMigrations());
        Assert.Equal(db.Database.GetMigrations(), applied);
    }

    [Fact]
    public void Model_HasNoChangesMissingFromTheMigrations()
    {
        using var db = Database.CreateContext(userId: null);

        Assert.False(db.Database.HasPendingModelChanges(), "Run 'dotnet ef migrations add' (docs/database/migrations.md).");
    }

    [Fact]
    public void EveryOwnedEntity_HasTheUserScopeFilter()
    {
        using var db = Database.CreateContext(userId: null);

        var unfiltered = db.Model.GetEntityTypes()
            .Where(type => typeof(IOwnedByUser).IsAssignableFrom(type.ClrType))
            .Where(type => type.GetDeclaredQueryFilters().All(filter => filter.Key != QueryFilters.UserScope))
            .Select(type => type.DisplayName());

        Assert.Empty(unfiltered);
    }

    [Fact]
    public void EveryOwnedEntity_HasAUniqueKeyOnUserAndId()
    {
        using var db = Database.CreateContext(userId: null);

        var withoutKey = db.Model.GetEntityTypes()
            .Where(type => typeof(OwnedEntity).IsAssignableFrom(type.ClrType))
            .Where(type => type.FindKey([
                type.FindProperty(nameof(OwnedEntity.UserId))!,
                type.FindProperty(nameof(OwnedEntity.Id))!,
            ]) is null)
            .Select(type => type.DisplayName());

        Assert.Empty(withoutKey);
    }

    [Fact]
    public void Tables_AreSnakeCase()
    {
        using var db = Database.CreateContext(userId: null);

        var tables = db.Model.GetEntityTypes().Select(type => type.GetTableName()).ToHashSet();

        Assert.Superset(
            new HashSet<string?> { "users", "accounts", "categories", "transactions", "transfers", "tags", "transaction_tags", "budgets" },
            tables);
    }
}
