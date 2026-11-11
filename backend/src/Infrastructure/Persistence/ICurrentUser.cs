namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>
/// The user the code is acting for. Reads are filtered by it and writes are checked against it
/// (docs/database/user-isolation.md). The HTTP implementation lives in Api ([Backend]).
/// </summary>
public interface ICurrentUser
{
    /// <summary>Null when nobody is acting: queries on user data return nothing and changes are refused.</summary>
    Guid? UserId { get; }
}

/// <summary>
/// A current user set explicitly: creation of default data for a new user, tests, the future Worker, design-time tools.
/// </summary>
public sealed class FixedCurrentUser(Guid? userId) : ICurrentUser
{
    public static FixedCurrentUser None { get; } = new(null);

    public Guid? UserId { get; } = userId;
}
