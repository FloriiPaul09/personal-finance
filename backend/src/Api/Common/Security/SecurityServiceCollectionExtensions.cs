using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PersonalFinance.Infrastructure.Identity;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Common.Security;

public static class SecurityServiceCollectionExtensions
{
    /// <summary>Name of the rate limiting policy of login and registration.</summary>
    public const string AuthRateLimitPolicy = "auth";

    /// <summary>Name of the session cookie.</summary>
    public const string SessionCookieName = "pf_session";

    /// <summary>
    /// Everything that protects the API (ADR-0001, ADR-0012, ADR-0014, docs/backend/security.md): Identity with the
    /// session cookie, the fallback policy, the HTTP current user, antiforgery, login rate limiting, Data Protection
    /// keys in the database and the <c>Auth</c> options validated at startup.
    /// </summary>
    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AuthOptions>, AuthOptionsValidator>());

        services.AddHttpContextAccessor();

        // AddPersistence registers "nobody" with TryAdd: replacing works whatever the registration order.
        services.Replace(ServiceDescriptor.Scoped<ICurrentUser, HttpCurrentUser>());

        AddSession(services);
        AddAntiforgery(services);
        AddLoginRateLimiting(services);

        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    /// <summary>Authentication, authorization, rate limiting and antiforgery, in this order, after routing.</summary>
    public static IApplicationBuilder UseApiSecurity(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        // Validates form and file endpoints (future CSV import, attachments); JSON endpoints use AntiforgeryValidationFilter.
        app.UseAntiforgery();
        return app;
    }

    private static void AddSession(IServiceCollection services)
    {
        services.AddIdentityStores().AddSignInManager();

        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = SessionCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;

            // Sliding: an active user stays signed in. Without "remember me" the browser drops the cookie when closed.
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;

            options.Events.OnRedirectToLogin = context => RespondWith(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => RespondWith(context, StatusCodes.Status403Forbidden);
        });

        // A password change ends the other sessions within this interval.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(5));

        // Keys that protect the cookies live in the database: sessions survive restarts and work across instances.
        services
            .AddDataProtection()
            .SetApplicationName("PersonalFinance")
            .PersistKeysToDbContext<AppDbContext>();
    }

    private static void AddAntiforgery(IServiceCollection services) =>
        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryCookie.HeaderName;
            options.Cookie.Name = "pf_antiforgery";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

    // Per client address, on top of the per-account lockout. Behind a reverse proxy the address comes from the
    // forwarded headers ([DevOps]); until then every client shares one partition.
    private static void AddLoginRateLimiting(IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimitPolicy, httpContext =>
            {
                var limit = httpContext.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.LoginRateLimit;
                return RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit.PermitLimit,
                        Window = TimeSpan.FromSeconds(limit.WindowSeconds),
                        QueueLimit = 0,
                    });
            });
        });

    // An API never redirects to a login page: the Angular app shows it when it receives 401 ([Frontend]).
    private static Task RespondWith(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
