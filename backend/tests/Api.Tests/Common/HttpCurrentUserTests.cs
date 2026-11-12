using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PersonalFinance.Api.Common.Security;

namespace PersonalFinance.Api.Tests.Common;

// The user the query filter and the interceptor act for (docs/backend/security.md#current-user).
public sealed class HttpCurrentUserTests
{
    [Fact]
    public void SignedInUser_IsTheIdInTheNameIdentifierClaim()
    {
        var userId = Guid.CreateVersion7();

        Assert.Equal(userId, CurrentUser(SignedIn(userId.ToString())).UserId);
    }

    [Fact]
    public void AnonymousRequest_HasNoUser()
    {
        Assert.Null(CurrentUser(new ClaimsPrincipal(new ClaimsIdentity())).UserId);
    }

    [Fact]
    public void NoRequest_HasNoUser()
    {
        Assert.Null(new HttpCurrentUser(new HttpContextAccessor()).UserId);
    }

    [Fact]
    public void UnauthenticatedIdentityWithAnId_HasNoUser()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString()) }));

        Assert.Null(CurrentUser(principal).UserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void SignedInWithoutAValidId_HasNoUser(string? claimValue)
    {
        Assert.Null(CurrentUser(SignedIn(claimValue)).UserId);
    }

    [Fact]
    public void UserIsReadOnEveryAccess()
    {
        var context = new DefaultHttpContext();
        var currentUser = new HttpCurrentUser(new HttpContextAccessor { HttpContext = context });
        var userId = Guid.CreateVersion7();

        Assert.Null(currentUser.UserId);
        context.User = SignedIn(userId.ToString());
        Assert.Equal(userId, currentUser.UserId);
    }

    private static HttpCurrentUser CurrentUser(ClaimsPrincipal principal) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });

    private static ClaimsPrincipal SignedIn(string? userId)
    {
        var claims = userId is null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }
}
