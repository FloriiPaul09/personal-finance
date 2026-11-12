using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Security;

namespace PersonalFinance.Api.Features.Auth;

/// <summary>
/// <c>GET /api/auth/antiforgery</c>: sets the <c>XSRF-TOKEN</c> cookie. Called by the app at startup, before the first
/// unsafe request (login included).
/// </summary>
public static class GetAntiforgeryToken
{
    internal sealed class Endpoint : IEndpoint<AuthModule>
    {
        public void Map(IEndpointRouteBuilder module) =>
            module.MapGet("antiforgery", Handle)
                .WithName(nameof(GetAntiforgeryToken))
                .AllowAnonymous();
    }

    internal static NoContent Handle(HttpContext httpContext, IAntiforgery antiforgery)
    {
        AntiforgeryCookie.Issue(httpContext, antiforgery);
        return TypedResults.NoContent();
    }
}
