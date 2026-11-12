using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Common.Security;

public static class CurrentUserExtensions
{
    /// <summary>
    /// The signed-in user, for slices that create data. The fallback policy guarantees one on every non-public
    /// endpoint, so a missing user is a bug and gives <c>500</c>.
    /// </summary>
    public static Guid RequiredUserId(this ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new InvalidOperationException("No signed-in user: the endpoint must not be public.");
}
