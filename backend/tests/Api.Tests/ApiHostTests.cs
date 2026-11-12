using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Security;
using PersonalFinance.Api.Tests.Support;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Tests;

// The real API host: pipeline, public endpoints, development tools (docs/backend/testing.md#host-tests).
public sealed class ApiHostTests(ApiServer server) : ApiTest(server)
{
    /// <summary>
    /// The only routes reachable without a session. A new public endpoint must be added here on purpose
    /// (docs/backend/security.md#public-endpoints).
    /// </summary>
    private static readonly string[] PublicRoutePrefixes =
        ["/api/health", "/api/auth/antiforgery", "/api/auth/login", "/api/auth/register", "/openapi/", "/scalar"];

    [Fact]
    public async Task Health_IsPublic()
    {
        var response = await AnonymousClient().GetAsync("/api/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnknownApiRoute_Returns401ProblemWithoutSession()
    {
        var response = await AnonymousClient().GetAsync("/api/does-not-exist", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(ApiErrors.AuthenticationRequired, problem.GetProperty("code").GetString());
    }

    [Fact]
    public void EveryUpdateAndDelete_RequiresIfMatch()
    {
        var unprotected = Host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods
                .Any(method => method is "PUT" or "PATCH" or "DELETE") == true)
            .Where(endpoint => endpoint.Metadata.GetMetadata<IfMatchRequiredMetadata>() is null)
            .Select(endpoint => endpoint.DisplayName);

        // Lost updates between phone and computer are refused only if the client must send the version (ADR-0013).
        Assert.Empty(unprotected);
    }

    [Fact]
    public void OnlyTheListedEndpoints_ArePublic()
    {
        var publicRoutes = Host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToList();

        Assert.Contains("/api/health", publicRoutes);
        Assert.All(publicRoutes, route =>
            Assert.Contains(PublicRoutePrefixes, prefix => route.StartsWith(prefix, StringComparison.Ordinal)));
    }

    [Fact]
    public void CurrentUser_IsTheSignedInUserOfTheRequest()
    {
        using var scope = Host.Services.CreateScope();

        Assert.IsType<HttpCurrentUser>(scope.ServiceProvider.GetRequiredService<ICurrentUser>());
    }

    [Fact]
    public async Task OpenApiDocument_IsServedInDevelopment()
    {
        var response = await AnonymousClient().GetAsync("/openapi/v1.json", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"openapi\"", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScalarUi_IsServedInDevelopment()
    {
        var response = await AnonymousClient().GetAsync("/scalar/", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/")]
    public async Task DevelopmentTools_DoNotExistInProduction(string route)
    {
        await using var production = new ApiFactory(Database, Environments.Production);

        var response = await production.CreateApiClient().GetAsync(route, Ct);

        // No endpoint: an anonymous caller gets 401 from the fallback policy, like any unknown route.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
