using System.Globalization;

namespace PersonalFinance.Api.Common.Concurrency;

/// <summary>
/// The version of a row as an HTTP entity tag: strong, quoted, e.g. <c>"12345"</c> (RFC 9110). Weak tags are never
/// accepted: <c>If-Match</c> compares tags strongly.
/// </summary>
public static class EntityTag
{
    public static string Format(uint version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    public static bool TryParse(string? value, out uint version)
    {
        version = 0;
        if (value is not { Length: > 2 } || value[0] != '"' || value[^1] != '"')
        {
            return false;
        }

        var digits = value.AsSpan(1, value.Length - 2);
        foreach (var character in digits)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return uint.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }
}
