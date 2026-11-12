using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Security;
using PersonalFinance.Infrastructure.Identity;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Auth;

/// <summary>
/// <c>POST /api/auth/register</c>: exists only when <c>Auth:AllowRegistration</c> is <c>true</c> (ADR-0001). Creates the
/// user with the default categories and starts its session.
/// </summary>
public static class Register
{
    public sealed record Request(
        [Required, EmailAddress, MaxLength(256)] string Email,
        [Required, MaxLength(IdentityPolicy.PasswordMaxLength)] string Password);

    internal sealed class Endpoint : IEndpoint<AuthModule>
    {
        public void Map(IEndpointRouteBuilder module)
        {
            // Read at startup: switching registration on or off needs a restart, and while off the route does not exist.
            if (!module.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value.AllowRegistration)
            {
                return;
            }

            module.MapPost("register", Handle)
                .WithName(nameof(Register))
                .AllowAnonymous()
                .RequireRateLimiting(SecurityServiceCollectionExtensions.AuthRateLimitPolicy);
        }
    }

    internal static async Task<Created<GetCurrentUser.Response>> Handle(
        Request request,
        SignInManager<ApplicationUser> signInManager,
        DefaultCategoriesInitializer defaultCategories,
        IAntiforgery antiforgery,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = new ApplicationUser { UserName = email, Email = email };

        var result = await signInManager.UserManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                throw new ApiProblemException(StatusCodes.Status409Conflict, ApiErrors.EmailTaken, "An account with this email already exists.", "email");
            }

            throw PasswordRules.IsAboutPassword(result)
                ? PasswordRules.Rejected(result, "password")
                : new ApiProblemException(
                    StatusCodes.Status400BadRequest,
                    ApiErrors.ValidationFailed,
                    string.Join(" ", result.Errors.Select(error => error.Description)),
                    "email");
        }

        await defaultCategories.EnsureForUserAsync(user.Id, cancellationToken);
        await signInManager.SignInAsync(user, isPersistent: false);

        httpContext.User = await signInManager.CreateUserPrincipalAsync(user);
        AntiforgeryCookie.Issue(httpContext, antiforgery);

        return TypedResults.Created("/api/auth/me", GetCurrentUser.ToResponse(user));
    }
}
