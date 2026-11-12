using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Security;

namespace PersonalFinance.Api.Tests.Common;

// Module groups and the fallback policy on a minimal host with sample endpoints (docs/backend/testing.md#mechanism-tests).
// The real host is covered by ApiHostTests.
public sealed class EndpointMappingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Endpoint_IsServedUnderApiAndModulePrefix()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().GetAsync("/api/samples/public", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task EndpointWithoutAllowAnonymous_Returns401WithoutSession()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().GetAsync("/api/samples/private", Ct);

        // 401, never a redirect to a login page.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_Returns401WithoutSession()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().GetAsync("/api/does-not-exist", Ct);

        // The fallback policy also covers requests that match no endpoint: routes are not revealed to anonymous users.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EveryEndpoint_HasTheTagOfItsModule()
    {
        await using var app = await StartAsync();

        var endpoints = SampleEndpoints(app);

        Assert.Equal(2, endpoints.Count);
        Assert.All(endpoints, endpoint =>
            Assert.Equal(new[] { "Samples" }, endpoint.Metadata.GetRequiredMetadata<ITagsMetadata>().Tags));
    }

    [Fact]
    public async Task ModuleConventions_ApplyToEveryEndpoint()
    {
        await using var app = await StartAsync();

        Assert.All(SampleEndpoints(app), endpoint => Assert.NotNull(endpoint.Metadata.GetMetadata<SampleMarker>()));
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddApiSecurity(builder.Configuration);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapModules(EndpointDiscovery.FromTypes([typeof(SamplesModule), typeof(PublicSample), typeof(PrivateSample)]));

        await app.StartAsync(Ct);
        return app;
    }

    private static List<RouteEndpoint> SampleEndpoints(WebApplication app) =>
        app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/samples", StringComparison.Ordinal) == true)
            .ToList();

    private sealed class SampleMarker;

    private sealed class SamplesModule : ApiModule
    {
        public override string Prefix => "samples";

        public override string Tag => "Samples";

        public override void Configure(RouteGroupBuilder group) => group.WithMetadata(new SampleMarker());
    }

    private sealed class PublicSample : IEndpoint<SamplesModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("public", () => "public").AllowAnonymous();
    }

    private sealed class PrivateSample : IEndpoint<SamplesModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("private", () => "private");
    }
}
