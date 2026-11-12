using Microsoft.AspNetCore.Identity;
using PersonalFinance.Api.Common.Errors;

namespace PersonalFinance.Api.Features.Auth;

internal static class PasswordRules
{
    /// <summary>Identity names every password rule error <c>Password…</c> (too short, mismatch, …).</summary>
    public static bool IsAboutPassword(IdentityResult result) =>
        result.Errors.All(error => error.Code.StartsWith("Password", StringComparison.Ordinal));

    /// <summary>A password refused by <c>IdentityPolicy</c> (e.g. too short), reported on <paramref name="field"/>.</summary>
    public static ApiProblemException Rejected(IdentityResult result, string field) => new(
        StatusCodes.Status400BadRequest,
        ApiErrors.PasswordRejected,
        string.Join(" ", result.Errors.Select(error => error.Description)),
        field);
}
