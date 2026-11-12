using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Security;
using PersonalFinance.Api.Features.Auth;
using PersonalFinance.Api.Tests.Support;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Testing;

namespace PersonalFinance.Api.Tests.Features.Auth;

// Registration, mapped only with Auth:AllowRegistration=true (docs/backend/api-endpoints.md#authentication).
public sealed class RegistrationTests(ApiServer server) : ApiTest(server)
{
    private const string Password = "a-long-enough-password";

    [Fact]
    public async Task Register_CreatesTheUser_WithTheDefaultCategories_AndStartsTheSession()
    {
        await using var host = HostWithRegistration();
        var client = host.CreateApiClient();
        var email = NewEmail();

        var response = await client.PostAsJsonAsync("/api/auth/register", new Register.Request(email, Password), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = (await response.Content.ReadFromJsonAsync<GetCurrentUser.Response>(Ct))!;
        Assert.Equal(email, user.Email);
        Assert.Equal(user, await client.GetFromJsonAsync<GetCurrentUser.Response>("/api/auth/me", Ct));

        await using var db = Database.CreateContext(user.Id);
        Assert.Equal(DefaultCategories.CreateFor(user.Id).Count, await db.Categories.CountAsync(Ct));
    }

    [Fact]
    public async Task Register_WithAnEmailInUse_Returns409()
    {
        await using var host = HostWithRegistration();
        var email = NewEmail();
        await host.CreateApiClient().PostAsJsonAsync("/api/auth/register", new Register.Request(email, Password), Ct);

        var response = await host.CreateApiClient().PostAsJsonAsync(
            "/api/auth/register", new Register.Request(email.ToUpperInvariant(), Password), Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.Conflict, ApiErrors.EmailTaken);
        Problems.AssertField(problem, "email");
    }

    [Fact]
    public async Task Register_WithAShortPassword_Returns400OnThePassword()
    {
        await using var host = HostWithRegistration();

        var response = await host.CreateApiClient().PostAsJsonAsync(
            "/api/auth/register", new Register.Request(NewEmail(), "short"), Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.PasswordRejected);
        Problems.AssertField(problem, "password");
    }

    [Fact]
    public async Task Register_IsPublicAndRateLimited()
    {
        await using var host = HostWithRegistration();

        var register = host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == "/api/auth/register");

        Assert.NotNull(register.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.Equal(
            SecurityServiceCollectionExtensions.AuthRateLimitPolicy,
            register.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
    }

    private ApiFactory HostWithRegistration() =>
        new(Database, Environments.Development, new Dictionary<string, string?> { ["Auth:AllowRegistration"] = "true" });

    private static string NewEmail() => $"new-{Guid.NewGuid():N}@example.com";
}
