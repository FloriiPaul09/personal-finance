using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Security;
using PersonalFinance.Api.Features.Auth;
using PersonalFinance.Api.Tests.Support;
using PersonalFinance.Infrastructure.Identity;

namespace PersonalFinance.Api.Tests.Features.Auth;

// Login, logout, current user, password change, CSRF and rate limiting through the real session cookie
// (docs/backend/api-endpoints.md#authentication, docs/backend/security.md).
public sealed class AuthTests(ApiServer server) : ApiTest(server)
{
    private const string NewPassword = "another-long-password";

    // ---- Login -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Login_StartsASession_SeenByMe()
    {
        var user = await NewUserWithPasswordAsync();
        var client = AnonymousClient();

        var login = await LoginAsync(client, user.Email, user.Password);
        var me = await client.GetFromJsonAsync<GetCurrentUser.Response>("/api/auth/me", Ct);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(new GetCurrentUser.Response(user.Id, user.Email), await login.Content.ReadFromJsonAsync<GetCurrentUser.Response>(Ct));
        Assert.Equal(user.Id, me!.Id);
    }

    [Fact]
    public async Task Login_SessionCookie_IsHttpOnlySecureStrict_AndEndsWithTheBrowser()
    {
        var user = await NewUserWithPasswordAsync();

        var login = await LoginAsync(AnonymousClient(), user.Email, user.Password);

        var cookie = SetCookie(login, SecurityServiceCollectionExtensions.SessionCookieName);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithRememberMe_KeepsTheCookie()
    {
        var user = await NewUserWithPasswordAsync();

        var login = await LoginAsync(AnonymousClient(), user.Email, user.Password, rememberMe: true);

        Assert.Contains("expires=", SetCookie(login, SecurityServiceCollectionExtensions.SessionCookieName), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithAWrongPasswordOrAnUnknownEmail_GivesTheSameAnswer()
    {
        var user = await NewUserWithPasswordAsync();
        var client = AnonymousClient();

        var wrongPassword = await LoginAsync(client, user.Email, "not-the-right-password");
        var unknownEmail = await LoginAsync(client, $"nobody-{Guid.NewGuid():N}@example.com", user.Password);

        await Problems.AssertAsync(wrongPassword, HttpStatusCode.Unauthorized, ApiErrors.InvalidCredentials);
        await Problems.AssertAsync(unknownEmail, HttpStatusCode.Unauthorized, ApiErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Login_LocksTheAccountAfterFiveFailures()
    {
        var user = await NewUserWithPasswordAsync();
        var client = AnonymousClient();

        for (var attempt = 0; attempt < IdentityPolicy.MaxFailedAccessAttempts; attempt++)
        {
            await LoginAsync(client, user.Email, "not-the-right-password");
        }

        var withTheRightPassword = await LoginAsync(client, user.Email, user.Password);

        await Problems.AssertAsync(withTheRightPassword, HttpStatusCode.Unauthorized, ApiErrors.InvalidCredentials);
    }

    [Fact]
    public async Task Login_WithAnInvalidEmail_Returns400OnTheField()
    {
        var response = await LoginAsync(AnonymousClient(), "not-an-email", "whatever-password");

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed);
        Problems.AssertField(problem, "email");
    }

    [Fact]
    public void Login_IsPublicAndRateLimited()
    {
        var login = Endpoint("/api/auth/login");

        Assert.NotNull(login.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>());
        Assert.Equal(
            SecurityServiceCollectionExtensions.AuthRateLimitPolicy,
            login.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
    }

    [Fact]
    public async Task Login_IsRateLimitedPerClientAddress()
    {
        await using var host = new ApiFactory(Database, Environments.Development, new Dictionary<string, string?>
        {
            ["Auth:LoginRateLimit:PermitLimit"] = "3",
            ["Auth:LoginRateLimit:WindowSeconds"] = "3600",
        });
        var client = host.CreateApiClient();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await LoginAsync(client, "someone@example.com", "not-the-right-password");
        }

        var rejected = await LoginAsync(client, "someone@example.com", "not-the-right-password");

        await Problems.AssertAsync(rejected, HttpStatusCode.TooManyRequests, ApiErrors.TooManyRequests);
    }

    // ---- CSRF --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task UnsafeRequestWithoutAntiforgeryToken_Returns400()
    {
        // A client without the antiforgery handler, like a page of another site posting to the API.
        var client = Host.CreateClient(new() { BaseAddress = TestClients.BaseAddress });

        var response = await client.PostAsJsonAsync("/api/auth/login", new Login.Request("me@example.com", "whatever-password"), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.AntiforgeryInvalid);
    }

    [Fact]
    public async Task AntiforgeryToken_IsAReadableStrictCookie()
    {
        var response = await AnonymousClient().GetAsync("/api/auth/antiforgery", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = SetCookie(response, AntiforgeryCookie.CookieName);
        Assert.DoesNotContain("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Antiforgery_WorksOverPlainHttp_ForLocalDevelopment()
    {
        // Regression: with the antiforgery cookie always Secure, ASP.NET Core threw on every plain-HTTP request (500).
        var user = await NewUserWithPasswordAsync();
        var client = Host.CreatePlainHttpClient();

        var token = await client.GetAsync("/api/auth/antiforgery", Ct);
        var login = await LoginAsync(client, user.Email, user.Password);

        Assert.Equal(HttpStatusCode.NoContent, token.StatusCode);
        Assert.DoesNotContain("secure", SetCookie(token, AntiforgeryCookie.CookieName), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // The session cookie stays Secure even here: browsers accept it only on localhost.
        Assert.Contains("secure", SetCookie(login, SecurityServiceCollectionExtensions.SessionCookieName), StringComparison.OrdinalIgnoreCase);
    }

    // ---- Logout and current user -------------------------------------------------------------------------------

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var user = await NewUserWithPasswordAsync();
        var client = AnonymousClient();
        await LoginAsync(client, user.Email, user.Password);

        var logout = await client.PostAsync("/api/auth/logout", content: null, Ct);
        var me = await client.GetAsync("/api/auth/me", Ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await Problems.AssertAsync(me, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Fact]
    public async Task Me_WithoutSession_Returns401()
    {
        var response = await AnonymousClient().GetAsync("/api/auth/me", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Fact]
    public async Task Me_ReturnsTheUserOfTheRequest_NotAnother()
    {
        var first = await NewUserWithPasswordAsync();
        var second = await NewUserWithPasswordAsync();

        var me = await ClientFor(second.Id).GetFromJsonAsync<GetCurrentUser.Response>("/api/auth/me", Ct);

        Assert.Equal(new GetCurrentUser.Response(second.Id, second.Email), me);
        Assert.NotEqual(first.Id, me!.Id);
    }

    [Fact]
    public async Task Session_SurvivesARestart_AndWorksOnAnotherInstance()
    {
        var user = await NewUserWithPasswordAsync();
        var login = await LoginAsync(AnonymousClient(), user.Email, user.Password);
        var session = SetCookie(login, SecurityServiceCollectionExtensions.SessionCookieName).Split(';')[0];

        // Another host on the same database: same Data Protection keys, read from the database.
        await using var otherInstance = new ApiFactory(Database);
        var client = otherInstance.CreateClient(new() { BaseAddress = TestClients.BaseAddress });
        client.DefaultRequestHeaders.Add("Cookie", session);
        var me = await client.GetAsync("/api/auth/me", Ct);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    // ---- Change password ---------------------------------------------------------------------------------------

    [Fact]
    public async Task ChangePassword_ReplacesThePassword()
    {
        var user = await NewUserWithPasswordAsync();
        var client = AnonymousClient();
        await LoginAsync(client, user.Email, user.Password);

        var change = await client.PostAsJsonAsync(
            "/api/auth/change-password", new ChangePassword.Request(user.Password, NewPassword), Ct);

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(AnonymousClient(), user.Email, user.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(AnonymousClient(), user.Email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithAWrongCurrentPassword_Returns400OnTheField()
    {
        var user = await NewUserWithPasswordAsync();

        var response = await ClientFor(user.Id).PostAsJsonAsync(
            "/api/auth/change-password", new ChangePassword.Request("not-the-right-password", NewPassword), Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.PasswordIncorrect);
        Problems.AssertField(problem, "currentPassword");
    }

    [Fact]
    public async Task ChangePassword_TooShort_Returns400OnTheNewPassword()
    {
        var user = await NewUserWithPasswordAsync();

        var response = await ClientFor(user.Id).PostAsJsonAsync(
            "/api/auth/change-password", new ChangePassword.Request(user.Password, "short"), Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.PasswordRejected);
        Problems.AssertField(problem, "newPassword");
    }

    [Fact]
    public async Task ChangePassword_WithoutSession_Returns401()
    {
        var response = await AnonymousClient().PostAsJsonAsync(
            "/api/auth/change-password", new ChangePassword.Request("whatever-password", NewPassword), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Registration ------------------------------------------------------------------------------------------

    [Fact]
    public void Register_DoesNotExist_WhileRegistrationIsOff()
    {
        Assert.DoesNotContain(Routes(), route => route == "/api/auth/register");
    }

    // ---- Helpers -----------------------------------------------------------------------------------------------

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password, bool rememberMe = false) =>
        client.PostAsJsonAsync("/api/auth/login", new Login.Request(email, password, rememberMe), TestContext.Current.CancellationToken);

    private static string SetCookie(HttpResponseMessage response, string name)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies), "No Set-Cookie header.");
        return Assert.Single(cookies, cookie => cookie.StartsWith(name + "=", StringComparison.Ordinal));
    }

    private RouteEndpoint Endpoint(string route) =>
        Host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == route);

    private List<string?> Routes() =>
        Host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();
}
