using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Infrastructure.Identity;

namespace PersonalFinance.Api.Features.Auth;

/// <summary>
/// <c>POST /api/auth/change-password</c>: needs the current password. The security stamp changes, so the other
/// sessions end within the stamp validation interval; this one is renewed.
/// </summary>
public static class ChangePassword
{
    public sealed record Request(
        [Required, MaxLength(IdentityPolicy.PasswordMaxLength)] string CurrentPassword,
        [Required, MaxLength(IdentityPolicy.PasswordMaxLength)] string NewPassword);

    internal sealed class Endpoint : IEndpoint<AuthModule>
    {
        public void Map(IEndpointRouteBuilder module) =>
            module.MapPost("change-password", Handle).WithName(nameof(ChangePassword));
    }

    internal static async Task<Results<NoContent, UnauthorizedHttpResult>> Handle(
        Request request, SignInManager<ApplicationUser> signInManager, HttpContext httpContext)
    {
        var userManager = signInManager.UserManager;
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            throw result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                ? new ApiProblemException(
                    StatusCodes.Status400BadRequest, ApiErrors.PasswordIncorrect, "The current password is wrong.", "currentPassword")
                : PasswordRules.Rejected(result, "newPassword");
        }

        await signInManager.RefreshSignInAsync(user);
        return TypedResults.NoContent();
    }
}
