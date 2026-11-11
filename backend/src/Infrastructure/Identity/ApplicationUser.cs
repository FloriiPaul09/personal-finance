using Microsoft.AspNetCore.Identity;

namespace PersonalFinance.Infrastructure.Identity;

/// <summary>
/// The account of a person using the application (table <c>users</c>). Domain entities reference it only through
/// <c>UserId</c> (ADR-0001, ADR-0010).
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Set by the SaveChanges interceptor.</summary>
    public DateTimeOffset CreatedAt { get; private set; }
}
