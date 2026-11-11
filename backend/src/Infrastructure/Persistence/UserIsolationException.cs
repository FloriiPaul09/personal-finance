namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>
/// A save would write data of a user other than the current one, change an owner, or write user data with no current
/// user. Always a programming error, never a user mistake.
/// </summary>
public sealed class UserIsolationException(string message) : InvalidOperationException(message);
