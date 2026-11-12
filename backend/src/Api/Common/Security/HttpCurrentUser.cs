using System.Security.Claims;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Common.Security;

/// <summary>
/// The signed-in user of the current request: the one the query filter <c>UserScope</c> reads for and the
/// interceptor writes for (docs/database/user-isolation.md, docs/backend/security.md#current-user).
/// </summary>
/// <remarks>
/// Read on every access, never cached: the same scope always sees the user of its own request. Anything that is not
/// a valid user id gives <see langword="null"/>, so queries on user data return nothing and changes are refused.
/// </remarks>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId => FromPrincipal(httpContextAccessor.HttpContext?.User);

    /// <summary>The id in the <see cref="ClaimTypes.NameIdentifier"/> claim, the one ASP.NET Core Identity issues.</summary>
    internal static Guid? FromPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var value = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var userId) && userId != Guid.Empty ? userId : null;
    }
}
