using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Identity;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Testing;

/// <summary>Contexts and users for tests (docs/database/testing.md#test-data-conventions).</summary>
public static class TestDatabaseExtensions
{
    /// <summary>A context acting for <paramref name="userId"/> (nobody when null), configured as in production.</summary>
    public static AppDbContext CreateContext(this TestDatabase database, Guid? userId, TimeProvider? clock = null) =>
        CreateContext(database.ConnectionString, userId, clock ?? TestClock.Default);

    /// <summary>Creates a user with a unique email and returns its id.</summary>
    public static async Task<Guid> CreateUserAsync(this TestDatabase database, CancellationToken cancellationToken = default)
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
        };

        await using var db = database.CreateContext(userId: null);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return user.Id;
    }

    internal static AppDbContext CreateContext(string connectionString, Guid? userId, TimeProvider clock)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UsePersonalFinanceDatabase(connectionString)
            .AddInterceptors(new OwnershipInterceptor(clock))
            .Options;

        return new AppDbContext(options, new FixedCurrentUser(userId));
    }
}
