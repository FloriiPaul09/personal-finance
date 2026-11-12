using Microsoft.AspNetCore.Identity;

namespace PersonalFinance.Infrastructure.Identity;

/// <summary>
/// Password, lockout and user rules (ADR-0014, docs/backend/security.md#password-and-lockout). Shared by every host
/// that creates users, so the initial user, registration and password changes follow the same rules.
/// </summary>
public static class IdentityPolicy
{
    /// <summary>Length matters more than composition (NIST SP 800-63B): no mandatory character classes.</summary>
    public const int PasswordMinLength = 12;

    /// <summary>Upper bound for request validation: long passphrases are fine, megabytes are not.</summary>
    public const int PasswordMaxLength = 128;

    public const int MaxFailedAccessAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static void Configure(IdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Password.RequiredLength = PasswordMinLength;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = MaxFailedAccessAttempts;
        options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;

        // The email is the user name: one account per address.
        options.User.RequireUniqueEmail = true;
    }
}
