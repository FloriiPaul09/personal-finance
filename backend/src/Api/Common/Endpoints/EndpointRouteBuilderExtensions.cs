using PersonalFinance.Api.Common.Security;

namespace PersonalFinance.Api.Common.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    /// <summary>Common prefix of every endpoint of the API; the frontend proxies it (docs/devops/local-development.md).</summary>
    public const string ApiPrefix = "/api";

    /// <summary>
    /// Maps every endpoint of this assembly on the group of its module (ADR-0003, ADR-0012). Adding a slice never
    /// requires a change here or in <c>Program.cs</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app) =>
        app.MapModules(EndpointDiscovery.FromAssembly(ApiAssembly.Reference));

    internal static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder app, IEnumerable<ModuleEndpoints> modules)
    {
        // Every unsafe request of every module carries the antiforgery token (docs/backend/security.md#csrf).
        var api = app.MapGroup(ApiPrefix);
        api.AddEndpointFilter<AntiforgeryValidationFilter>();

        foreach (var (module, endpoints) in modules)
        {
            var group = api.MapGroup("/" + module.Prefix).WithTags(module.Tag);
            module.Configure(group);

            foreach (var endpoint in endpoints)
            {
                endpoint.Map(group);
            }
        }

        return app;
    }
}
