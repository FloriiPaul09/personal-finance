using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Security;
using PersonalFinance.Infrastructure.Identity;

namespace PersonalFinance.Api.Features.Auth;

/// <summary><c>POST /api/auth/logout</c>: ends the session of this browser and issues an anonymous antiforgery token.</summary>
public static class Logout
{
    internal sealed class Endpoint : IEndpoint<AuthModule>
    {
        public void Map(IEndpointRouteBuilder module) =>
            module.MapPost("logout", Handle).WithName(nameof(Logout));
    }

    internal static async Task<NoContent> Handle(
        SignInManager<ApplicationUser> signInManager, IAntiforgery antiforgery, HttpContext httpContext)
    {
        await signInManager.SignOutAsync();

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        AntiforgeryCookie.Issue(httpContext, antiforgery);

        return TypedResults.NoContent();
    }
}
