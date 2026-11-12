using PersonalFinance.Api.Common.Security;

namespace PersonalFinance.Api.Tests.Common;

// Auth options are checked at startup (docs/devops/configuration.md, docs/backend/security.md).
public sealed class AuthOptionsValidatorTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        Assert.True(Validate(new AuthOptions()).Succeeded);
    }

    [Fact]
    public void InitialUserWithEmailAndPassword_IsValid()
    {
        var options = new AuthOptions { InitialUser = { Email = "me@example.com", Password = "a-long-enough-password" } };

        Assert.True(Validate(options).Succeeded);
    }

    [Theory]
    [InlineData("me@example.com", null)]
    [InlineData(null, "a-long-enough-password")]
    [InlineData("not-an-email", "a-long-enough-password")]
    public void IncompleteOrInvalidInitialUser_IsRejected(string? email, string? password)
    {
        var options = new AuthOptions { InitialUser = { Email = email, Password = password } };

        Assert.True(Validate(options).Failed);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(10, 0)]
    public void RateLimitOutOfRange_IsRejected(int permitLimit, int windowSeconds)
    {
        var options = new AuthOptions { LoginRateLimit = { PermitLimit = permitLimit, WindowSeconds = windowSeconds } };

        Assert.True(Validate(options).Failed);
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(AuthOptions options) =>
        new AuthOptionsValidator().Validate(name: null, options);
}
