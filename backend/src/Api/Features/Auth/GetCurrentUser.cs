using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Identity;

namespace PersonalFinance.Api.Features.Auth;

/// <summary><c>GET /api/auth/me</c>: who is signed in. The app calls it at startup to know whether to show the login.</summary>
public static class GetCurrentUser
{
    public sealed record Response(Guid Id, string Email);

    internal sealed class Endpoint : IEndpoint<AuthModule>
    {
        public void Map(IEndpointRouteBuilder module) =>
            module.MapGet("me", Handle).WithName(nameof(GetCurrentUser));
    }

    internal static async Task<Results<Ok<Response>, UnauthorizedHttpResult>> Handle(
        HttpContext httpContext, UserManager<ApplicationUser> userManager)
    {
        // A valid cookie of a user deleted in the meantime: treated as no session.
        var user = await userManager.GetUserAsync(httpContext.User);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(ToResponse(user));
    }

    internal static Response ToResponse(ApplicationUser user) => new(user.Id, user.Email ?? user.UserName ?? string.Empty);
}
