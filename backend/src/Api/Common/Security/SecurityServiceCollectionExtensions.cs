using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Common.Security;

public static class SecurityServiceCollectionExtensions
{
    /// <summary>
    /// Authentication, authorization and the current user of the API (ADR-0001, ADR-0012, docs/backend/security.md):
    /// <list type="bullet">
    /// <item>the ASP.NET Core Identity cookie, answering <c>401</c> / <c>403</c> instead of redirecting to a login page;</item>
    /// <item>a fallback policy that requires a signed-in user on every endpoint that does not say otherwise;</item>
    /// <item><see cref="HttpCurrentUser"/> as the <see cref="ICurrentUser"/> of the query filter and the interceptor.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Sign-in itself (Identity stores, sign-in manager, password and cookie settings, auth endpoints) comes with the
    /// authentication block (docs/backend/security.md#to-do--open-questions).
    /// </remarks>
    public static IServiceCollection AddApiSecurity(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        // AddPersistence registers "nobody" with TryAdd: replacing works whatever the registration order.
        services.Replace(ServiceDescriptor.Scoped<ICurrentUser, HttpCurrentUser>());

        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Events.OnRedirectToLogin = context => RespondWith(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => RespondWith(context, StatusCodes.Status403Forbidden);
        });

        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    // An API never redirects to a login page: the Angular app shows it when it receives 401 ([Frontend]).
    private static Task RespondWith(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
