namespace PersonalFinance.Domain.Common;

/// <summary>Rules for names, descriptions and currency codes.</summary>
public static class Texts
{
    /// <summary>A required name, trimmed.</summary>
    public static string EnsureName(string? name, int maxLength)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException(DomainErrors.NameRequired, "A name is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException(DomainErrors.NameTooLong, $"A name has at most {maxLength} characters.");
        }

        return trimmed;
    }

    /// <summary>
    /// The form used to compare names without regard to case, stored in <c>normalized_name</c>
    /// (docs/database/conventions.md#normalized-names).
    /// </summary>
    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    /// <summary>An optional description: trimmed, empty becomes null.</summary>
    public static string? EnsureDescription(string? description, int maxLength)
    {
        var trimmed = description?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException(DomainErrors.DescriptionTooLong, $"A description has at most {maxLength} characters.");
        }

        return trimmed;
    }

    /// <summary>An ISO 4217 code: three letters, stored in upper case.</summary>
    public static string EnsureCurrency(string? currency)
    {
        var code = currency?.Trim().ToUpperInvariant() ?? string.Empty;

        if (code.Length != 3 || !code.All(c => c is >= 'A' and <= 'Z'))
        {
            throw new DomainException(DomainErrors.CurrencyInvalid, "A currency is a three-letter ISO 4217 code.");
        }

        return code;
    }
}
