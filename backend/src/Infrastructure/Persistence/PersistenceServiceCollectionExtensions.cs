using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PersonalFinance.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AppDbContext"/> on PostgreSQL with the project conventions. The current user defaults to
    /// nobody: the API replaces it with the authenticated user ([Backend]).
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return services.AddPersistence(_ => connectionString);
    }

    /// <summary>
    /// Same as <see cref="AddPersistence(IServiceCollection, string)"/>, with the connection string read when a context
    /// is created. The API reads it from the final configuration, so test hosts can replace it (docs/backend/testing.md).
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, Func<IServiceProvider, string> connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<OwnershipInterceptor>();
        services.TryAddScoped<ICurrentUser>(_ => FixedCurrentUser.None);

        services.AddDbContext<AppDbContext>((provider, options) =>
            options
                .UsePersonalFinanceDatabase(connectionString(provider))
                .AddInterceptors(provider.GetRequiredService<OwnershipInterceptor>()));

        services.TryAddScoped<DefaultCategoriesInitializer>();

        return services;
    }

    /// <summary>
    /// PostgreSQL with snake_case names. The one place where the provider is configured: the API, the design-time tools
    /// and the tests all use it, so they build the same model.
    /// </summary>
    public static TBuilder UsePersonalFinanceDatabase<TBuilder>(this TBuilder options, string connectionString)
        where TBuilder : DbContextOptionsBuilder
    {
        options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return options;
    }

    /// <summary>Applies pending migrations (at API startup, docs/database/migrations.md).</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
