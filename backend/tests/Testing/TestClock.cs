namespace PersonalFinance.Testing;

/// <summary>A clock that always returns the same instant, so timestamps can be asserted exactly.</summary>
public sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public static TestClock Default { get; } = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));

    public override DateTimeOffset GetUtcNow() => now;
}
