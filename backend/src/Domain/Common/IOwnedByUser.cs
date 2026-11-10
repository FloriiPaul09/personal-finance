namespace PersonalFinance.Domain.Common;

/// <summary>
/// Data that belongs to one user. Every type implementing it is filtered by the current user and checked by the
/// SaveChanges interceptor (docs/database/user-isolation.md).
/// </summary>
public interface IOwnedByUser
{
    Guid UserId { get; }
}
