using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PersonalFinance.Infrastructure.Persistence.Configurations;

/// <summary>
/// Stores an enum as snake_case text (<c>CreditCard</c> → <c>credit_card</c>), guarded by a CHECK constraint
/// (docs/database/conventions.md#types).
/// </summary>
internal sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => ToText(value),
    text => FromText(text))
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> Texts =
        Enum.GetValues<TEnum>().ToDictionary(value => value, value => ToSnakeCase(value.ToString()));

    private static readonly Dictionary<string, TEnum> Values =
        Texts.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    /// <summary><c>column IN ('a', 'b', ...)</c> with every stored value.</summary>
    public static string CheckSql(string column) =>
        $"{column} IN ({string.Join(", ", Texts.Values.Select(text => $"'{text}'"))})";

    private static string ToText(TEnum value) => Texts[value];

    private static TEnum FromText(string text) => Values[text];

    private static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(name[i]));
        }

        return builder.ToString();
    }
}
