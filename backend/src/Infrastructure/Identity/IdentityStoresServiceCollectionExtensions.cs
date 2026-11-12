using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Identity;

public static class IdentityStoresServiceCollectionExtensions
{
    /// <summary>
    /// User management on <see cref="AppDbContext"/> with <see cref="IdentityPolicy"/>: <c>UserManager</c> and the
    /// initial user. No HTTP: the API adds sign-in and cookies on the returned builder, a future Worker can use it as is.
    /// </summary>
    public static IdentityBuilder AddIdentityStores(this IServiceCollection services)
    {
        services.TryAddScoped<InitialUserInitializer>();

        return services
            .AddIdentityCore<ApplicationUser>(IdentityPolicy.Configure)
            .AddEntityFrameworkStores<AppDbContext>();
    }
}
