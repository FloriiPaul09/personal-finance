using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PersonalFinance.Api.Tests.Support;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Testing;

namespace PersonalFinance.Api.Tests;

// The application refuses to run without an owner and creates the first user once (ADR-0001,
// docs/backend/security.md#initial-user). Each test uses a database of its own, cloned from the migrated template.
public sealed class StartupTests(ApiServer server) : ApiTest(server)
{
    private static readonly Dictionary<string, string?> NoInitialUser = new()
    {
        ["Auth:InitialUser:Email"] = "",
        ["Auth:InitialUser:Password"] = "",
    };

    [Fact]
    public async Task NoUsersAndNoInitialUser_StopStartup()
    {
        var database = await Server.CreateDatabaseAsync(Ct);
        await using var host = new ApiFactory(database, Environments.Production, NoInitialUser);

        var exception = Record.Exception(() => host.Services);

        Assert.Contains("Auth__InitialUser", Messages(exception), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoUsers_CreatesTheInitialUser_WithTheDefaultCategories()
    {
        var database = await Server.CreateDatabaseAsync(Ct);
        await using var host = new ApiFactory(database, Environments.Production);

        _ = host.Services;

        await using var db = database.CreateContext(userId: null);
        var user = await db.Users.SingleAsync(Ct);
        Assert.Equal(ApiFactory.InitialUserEmail, user.Email);

        await using var userDb = database.CreateContext(user.Id);
        Assert.Equal(DefaultCategories.CreateFor(user.Id).Count, await userDb.Categories.CountAsync(Ct));
    }

    [Fact]
    public async Task ExistingUser_InitialUserIsIgnored()
    {
        var database = await Server.CreateDatabaseAsync(Ct);
        var existing = await database.CreateUserAsync(Ct);
        await using var host = new ApiFactory(database, Environments.Production);

        _ = host.Services;

        await using var db = database.CreateContext(userId: null);
        Assert.Equal(existing, (await db.Users.SingleAsync(Ct)).Id);
    }

    [Fact]
    public async Task InitialUserWithAShortPassword_StopsStartup()
    {
        var database = await Server.CreateDatabaseAsync(Ct);
        await using var host = new ApiFactory(database, Environments.Production, new Dictionary<string, string?>
        {
            ["Auth:InitialUser:Password"] = "short",
        });

        var exception = Record.Exception(() => host.Services);

        Assert.Contains("Auth__InitialUser", Messages(exception), StringComparison.Ordinal);
        await using var db = database.CreateContext(userId: null);
        Assert.False(await db.Users.AnyAsync(Ct));
    }

    [Fact]
    public async Task InvalidAuthOptions_StopStartup()
    {
        var database = await Server.CreateDatabaseAsync(Ct);
        await using var host = new ApiFactory(database, Environments.Production, new Dictionary<string, string?>
        {
            ["Auth:InitialUser:Email"] = "not-an-email",
        });

        var exception = Record.Exception(() => host.Services);

        Assert.Contains("Auth:InitialUser:Email", Messages(exception), StringComparison.Ordinal);
    }

    /// <summary>The host may wrap the startup exception: every message of the chain.</summary>
    private static string Messages(Exception? exception)
    {
        Assert.NotNull(exception);

        var messages = new List<string>();
        var pending = new Stack<Exception>();
        pending.Push(exception);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            messages.Add(current.Message);
            if (current is AggregateException aggregate)
            {
                aggregate.InnerExceptions.ToList().ForEach(pending.Push);
            }
            else if (current.InnerException is not null)
            {
                pending.Push(current.InnerException);
            }
        }

        return string.Join(Environment.NewLine, messages);
    }
}
