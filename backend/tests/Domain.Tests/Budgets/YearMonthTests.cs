using PersonalFinance.Domain.Budgets;
using PersonalFinance.Domain.Common;

namespace PersonalFinance.Domain.Tests.Budgets;

public class YearMonthTests
{
    [Theory]
    [InlineData(2026, 1, 31)]
    [InlineData(2026, 4, 30)]
    [InlineData(2027, 2, 28)]
    [InlineData(2028, 2, 29)] // leap year
    [InlineData(2026, 12, 31)]
    public void LastDay_FollowsTheCalendar(int year, int month, int lastDay)
    {
        var yearMonth = new YearMonth(year, month);

        Assert.Equal(new DateOnly(year, month, 1), yearMonth.FirstDay);
        Assert.Equal(new DateOnly(year, month, lastDay), yearMonth.LastDay);
    }

    [Fact]
    public void Contains_IncludesFirstAndLastDayOnly()
    {
        var february = new YearMonth(2028, 2);

        Assert.True(february.Contains(new DateOnly(2028, 2, 1)));
        Assert.True(february.Contains(new DateOnly(2028, 2, 29)));
        Assert.False(february.Contains(new DateOnly(2028, 1, 31)));
        Assert.False(february.Contains(new DateOnly(2028, 3, 1)));
        Assert.False(february.Contains(new DateOnly(2027, 2, 15)));
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(0, 1)]
    public void Create_RejectsInvalidMonths(int year, int month) =>
        Rule.Violated(DomainErrors.MonthInvalid, () => new YearMonth(year, month));

    [Fact]
    public void Months_AreOrdered()
    {
        Assert.True(new YearMonth(2026, 12) < new YearMonth(2027, 1));
        Assert.True(new YearMonth(2026, 3) > new YearMonth(2026, 2));
        Assert.Equal(new YearMonth(2026, 3), YearMonth.Of(new DateOnly(2026, 3, 17)));
        Assert.Equal("2026-03", new YearMonth(2026, 3).ToString());
    }

    [Theory]
    [InlineData("2026-06", 2026, 6)]
    [InlineData("0001-01", 1, 1)]
    [InlineData("9999-12", 9999, 12)]
    public void TryParse_ReadsYearDashMonth(string text, int year, int month)
    {
        Assert.True(YearMonth.TryParse(text, out var parsed));
        Assert.Equal(new YearMonth(year, month), parsed);
        Assert.Equal(text, parsed.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-6")]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("0000-01")]
    [InlineData("2026/06")]
    [InlineData("2026-06-01")]
    [InlineData("+026-06")]
    [InlineData("2026-+6")]
    public void TryParse_RejectsAnythingElse(string? text) => Assert.False(YearMonth.TryParse(text, out _));
}
