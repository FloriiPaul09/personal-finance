using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PersonalFinance.Api.Tests.Support;

/// <summary>
/// Signs a request in as the user named in the <see cref="Header"/> header, without login or cookie (ADR-0014,
/// docs/backend/testing.md#authentication-in-tests). Resource tests use it; requests without the header go through
/// the real session cookie, which the authentication tests exercise.
/// </summary>
public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "Test";
    public const string Header = "X-Test-User";

    /// <summary>Picks the test scheme when the header is present, the real cookie otherwise.</summary>
    public const string SelectorScheme = "TestOrCookie";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Header, out var value) || !Guid.TryParse(value, out var userId))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Header {Header} must contain a user id."));
        }

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, Scheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }

    /// <summary>Registered by <see cref="ApiFactory"/> on top of the services of <c>Program.cs</c>.</summary>
    public static void AddTo(IServiceCollection services)
    {
        services
            .AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(Scheme, configureOptions: null)
            .AddPolicyScheme(SelectorScheme, displayName: null, options =>
                options.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(Header) ? Scheme : IdentityConstants.ApplicationScheme);

        services.Configure<AuthenticationOptions>(options => options.DefaultScheme = SelectorScheme);
    }
}
