using PersonalFinance.Domain.Common;

namespace PersonalFinance.Domain.Budgets;

/// <summary>A calendar month. Plain dates: no time zone is involved (docs/database/domain-model.md#budget-and-progress).</summary>
public readonly record struct YearMonth : IComparable<YearMonth>
{
    public YearMonth(int year, int month)
    {
        if (year is < 1 or > 9999 || month is < 1 or > 12)
        {
            throw new DomainException(DomainErrors.MonthInvalid, "Invalid month.");
        }

        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    public DateOnly FirstDay => new(Year, Month, 1);

    public DateOnly LastDay => new(Year, Month, DateTime.DaysInMonth(Year, Month));

    public static YearMonth Of(DateOnly date) => new(date.Year, date.Month);

    /// <summary>Reads the form of <see cref="ToString"/>, <c>yyyy-MM</c> (e.g. <c>2026-06</c>); nothing else.</summary>
    public static bool TryParse(string? text, out YearMonth month)
    {
        month = default;
        if (text is not { Length: 7 } || text[4] != '-'
            || !int.TryParse(text.AsSpan(0, 4), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(text.AsSpan(5, 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number)
            || year < 1 || number is < 1 or > 12)
        {
            return false;
        }

        month = new YearMonth(year, number);
        return true;
    }

    public static bool operator <(YearMonth left, YearMonth right) => left.CompareTo(right) < 0;

    public static bool operator >(YearMonth left, YearMonth right) => left.CompareTo(right) > 0;

    public static bool operator <=(YearMonth left, YearMonth right) => left.CompareTo(right) <= 0;

    public static bool operator >=(YearMonth left, YearMonth right) => left.CompareTo(right) >= 0;

    public bool Contains(DateOnly date) => date.Year == Year && date.Month == Month;

    public int CompareTo(YearMonth other) => (Year, Month).CompareTo((other.Year, other.Month));

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
