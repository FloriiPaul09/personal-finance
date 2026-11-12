using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace PersonalFinance.Api.Common.Security;

/// <summary>
/// Section <c>Auth</c> of the configuration (docs/devops/configuration.md), validated at startup: a wrong value stops
/// the application before it serves any request.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Maps <c>POST /api/auth/register</c>. Off by default (ADR-0001).</summary>
    public bool AllowRegistration { get; set; }

    public InitialUserOptions InitialUser { get; set; } = new();

    public RateLimitOptions LoginRateLimit { get; set; } = new();
}

/// <summary>First user, created when the database has no users; ignored afterwards.</summary>
public sealed class InitialUserOptions
{
    public string? Email { get; set; }

    public string? Password { get; set; }
}

/// <summary>Attempts per client address on login and registration, on top of the per-account lockout.</summary>
public sealed class RateLimitOptions
{
    public int PermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;
}

internal sealed class AuthOptionsValidator : IValidateOptions<AuthOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        var failures = new List<string>();

        var email = options.InitialUser.Email;
        var password = options.InitialUser.Password;
        if (string.IsNullOrWhiteSpace(email) != string.IsNullOrWhiteSpace(password))
        {
            failures.Add("Auth:InitialUser needs both Email and Password, or neither.");
        }

        if (!string.IsNullOrWhiteSpace(email) && !new EmailAddressAttribute().IsValid(email))
        {
            failures.Add("Auth:InitialUser:Email is not a valid email address.");
        }

        if (options.LoginRateLimit.PermitLimit is < 1 or > 100_000)
        {
            failures.Add("Auth:LoginRateLimit:PermitLimit must be between 1 and 100000.");
        }

        if (options.LoginRateLimit.WindowSeconds is < 1 or > 86_400)
        {
            failures.Add("Auth:LoginRateLimit:WindowSeconds must be between 1 and 86400.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
