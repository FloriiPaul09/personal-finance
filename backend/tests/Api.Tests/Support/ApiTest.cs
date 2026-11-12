using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Infrastructure.Identity;
using PersonalFinance.Testing;

namespace PersonalFinance.Api.Tests.Support;

/// <summary>
/// Base class of endpoint tests: the database of the test class (ADR-0011) and the API host running on it.
/// Every test creates its own users and never asserts on counts across the whole database.
/// </summary>
public abstract class ApiTest(ApiServer server) : DatabaseTest(server)
{
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The Development host of this test class, started on first use.</summary>
    protected ApiFactory Host => ((ApiServer)Server).HostForClass(GetType(), Database);

    /// <summary>A client without session, with cookies and antiforgery like the browser app.</summary>
    protected HttpClient AnonymousClient() => Host.CreateApiClient();

    /// <summary>A client signed in as <paramref name="userId"/> without login (docs/backend/testing.md#authentication-in-tests).</summary>
    protected HttpClient ClientFor(Guid userId) => Host.CreateApiClient(userId);

    /// <summary>A new user without password, for resource tests.</summary>
    protected Task<Guid> NewUserAsync() => Database.CreateUserAsync(Ct);

    /// <summary>A new user who can log in, created through Identity with the real password rules.</summary>
    protected async Task<TestUser> NewUserWithPasswordAsync(string password = "a-long-enough-password")
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";

        await using var scope = Host.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Description)));

        return new TestUser(user.Id, email, password);
    }
}

public sealed record TestUser(Guid Id, string Email, string Password);
