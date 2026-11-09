using System.Text.RegularExpressions;

namespace PersonalFinance.Api.Tests.Infrastructure;

// Guards the decision in ADR-0008: one pinned PostgreSQL version, shared by Compose and Testcontainers.
public partial class PostgresImageTests
{
    [Fact]
    public void Name_IsReadFromTheDbServiceInCompose() =>
        Assert.StartsWith("postgres:", PostgresImage.Name, StringComparison.Ordinal);

    [Fact]
    public void Name_PinsMajorAndMinorVersion() =>
        Assert.Matches(PinnedVersion(), PostgresImage.Name);

    [Fact]
    public void Name_IsNotAlpineBased() =>
        // Alpine (musl) sorts text differently from Debian (glibc): tests must match production.
        Assert.DoesNotContain("alpine", PostgresImage.Name, StringComparison.OrdinalIgnoreCase);

    // postgres:<major>.<minor>, optionally followed by a Debian suffix (e.g. -trixie).
    [GeneratedRegex(@"^postgres:\d+\.\d+(-[a-z]+)?$")]
    private static partial Regex PinnedVersion();
}
