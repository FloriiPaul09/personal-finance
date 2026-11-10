using PersonalFinance.Domain.Common;

namespace PersonalFinance.Domain.Tests.Common;

public class AmountsTests
{
    [Theory]
    [InlineData("0.01")]
    [InlineData("12.34")]
    [InlineData("12.340")] // trailing zeros are not extra decimals
    [InlineData("9999999999999999.99")]
    public void EnsurePositive_AcceptsPositiveAmountsWithTwoDecimals(string value)
    {
        var amount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(amount, Amounts.EnsurePositive(amount));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("-100")]
    public void EnsurePositive_RejectsZeroAndNegative(string value) =>
        Rule.Violated(DomainErrors.AmountNotPositive, () => Amounts.EnsurePositive(Parse(value)));

    [Theory]
    [InlineData("12.345")]
    [InlineData("0.001")]
    [InlineData("-1.005")]
    public void EnsureValid_RejectsMoreThanTwoDecimals_InsteadOfRounding(string value) =>
        Rule.Violated(DomainErrors.AmountTooManyDecimals, () => Amounts.EnsureValid(Parse(value)));

    [Theory]
    [InlineData("10000000000000000")]
    [InlineData("-10000000000000000")]
    public void EnsureValid_RejectsValuesBeyondNumeric18_2(string value) =>
        Rule.Violated(DomainErrors.AmountOutOfRange, () => Amounts.EnsureValid(Parse(value)));

    [Theory]
    [InlineData("0")]
    [InlineData("-250.50")]
    public void EnsureValid_AcceptsZeroAndNegative(string value) =>
        Assert.Equal(Parse(value), Amounts.EnsureValid(Parse(value)));

    private static decimal Parse(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
