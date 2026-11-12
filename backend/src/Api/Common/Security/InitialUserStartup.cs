using Microsoft.Extensions.Options;
using PersonalFinance.Infrastructure.Identity;

namespace PersonalFinance.Api.Common.Security;

public static class InitialUserStartup
{
    /// <summary>
    /// After the migrations, before serving requests: creates the user of <c>Auth:InitialUser</c> with the default
    /// categories when the database has no users, and stops startup when it has none and the values are missing
    /// (ADR-0001, docs/backend/security.md#initial-user).
    /// </summary>
    public static async Task EnsureInitialUserAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var initialUser = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value.InitialUser;
        var initializer = scope.ServiceProvider.GetRequiredService<InitialUserInitializer>();

        if (await initializer.EnsureAsync(initialUser.Email, initialUser.Password, cancellationToken))
        {
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(InitialUserStartup))
                .LogInformation("Initial user {Email} created with the default categories.", initialUser.Email);
        }
    }
}
