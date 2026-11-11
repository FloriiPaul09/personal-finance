using PersonalFinance.Domain.Common;

namespace PersonalFinance.Domain.Tests.Common;

public class TextsTests
{
    [Fact]
    public void EnsureName_TrimsTheName() => Assert.Equal("Spesa", Texts.EnsureName("  Spesa  ", 100));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureName_RejectsMissingNames(string? name) =>
        Rule.Violated(DomainErrors.NameRequired, () => Texts.EnsureName(name, 100));

    [Fact]
    public void EnsureName_RejectsNamesLongerThanTheLimit() =>
        Rule.Violated(DomainErrors.NameTooLong, () => Texts.EnsureName(new string('a', 101), 100));

    [Fact]
    public void NormalizeName_IgnoresCase() => Assert.Equal(Texts.NormalizeName("spesa "), Texts.NormalizeName("SPESA"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void EnsureDescription_TurnsEmptyIntoNull(string? description) =>
        Assert.Null(Texts.EnsureDescription(description, 200));

    [Fact]
    public void EnsureDescription_RejectsLongDescriptions() =>
        Rule.Violated(DomainErrors.DescriptionTooLong, () => Texts.EnsureDescription(new string('a', 201), 200));

    [Theory]
    [InlineData("EUR", "EUR")]
    [InlineData(" usd ", "USD")]
    public void EnsureCurrency_StoresUpperCaseCodes(string input, string expected) =>
        Assert.Equal(expected, Texts.EnsureCurrency(input));

    [Theory]
    [InlineData(null)]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    [InlineData("€€€")]
    public void EnsureCurrency_RejectsInvalidCodes(string? currency) =>
        Rule.Violated(DomainErrors.CurrencyInvalid, () => Texts.EnsureCurrency(currency));
}
