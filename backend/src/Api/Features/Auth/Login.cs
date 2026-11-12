using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Security;
using PersonalFinance.Infrastructure.Identity;

namespace PersonalFinance.Api.Features.Auth;

/// <summary>
/// <c>POST /api/auth/login</c>: starts a session (cookie <c>pf_session</c>). Wrong email, wrong password and locked
/// account give the same <c>401</c>; five failures lock the account for 15 minutes; the client address is rate limited.
/// </summary>
public static class Login
{
    public sealed record Request(
        [Required, EmailAddress, MaxLength(256)] string Email,
        [Required, MaxLength(IdentityPolicy.PasswordMaxLength)] string Password,
        bool RememberMe = false);

    internal sealed class Endpoint : IEndpoint<AuthModule>
    {
        public void Map(IEndpointRouteBuilder module) =>
            module.MapPost("login", Handle)
                .WithName(nameof(Login))
                .AllowAnonymous()
                .RequireRateLimiting(SecurityServiceCollectionExtensions.AuthRateLimitPolicy);
    }

    internal static async Task<Ok<GetCurrentUser.Response>> Handle(
        Request request, SignInManager<ApplicationUser> signInManager, IAntiforgery antiforgery, HttpContext httpContext)
    {
        var result = await signInManager.PasswordSignInAsync(
            request.Email.Trim(), request.Password, request.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            throw ApiProblemException.InvalidCredentials();
        }

        var user = await signInManager.UserManager.FindByNameAsync(request.Email.Trim())
            ?? throw ApiProblemException.InvalidCredentials();

        // The antiforgery token is bound to the user: the anonymous one sent with this request is no longer valid.
        httpContext.User = await signInManager.CreateUserPrincipalAsync(user);
        AntiforgeryCookie.Issue(httpContext, antiforgery);

        return TypedResults.Ok(GetCurrentUser.ToResponse(user));
    }
}
