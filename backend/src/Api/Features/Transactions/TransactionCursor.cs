using System.Buffers.Text;
using System.Globalization;
using System.Text;
using PersonalFinance.Api.Common.Errors;

namespace PersonalFinance.Api.Features.Transactions;

/// <summary>
/// Position in the transaction list: the date and id of the last item of a page (keyset paging, ADR-0017). Opaque to
/// clients; the format can change without breaking them, since they only pass back what they received.
/// </summary>
internal readonly record struct TransactionCursor(DateOnly Date, Guid Id)
{
    private const string DateFormat = "yyyy-MM-dd";

    public string Encode() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{Date.ToString(DateFormat, CultureInfo.InvariantCulture)}_{Id:N}"));

    /// <summary>The cursor in <paramref name="value"/>; anything the API did not issue gives <c>400</c> on <c>cursor</c>.</summary>
    public static TransactionCursor? Decode(string? value, string field)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            var text = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));
            var parts = text.Split('_');
            if (parts.Length == 2
                && DateOnly.TryParseExact(parts[0], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                && Guid.TryParseExact(parts[1], "N", out var id))
            {
                return new TransactionCursor(date, id);
            }
        }
        catch (FormatException)
        {
            // Not Base64Url: same answer as a malformed content.
        }

        throw ApiProblemException.UnreadableQuery(field, "Pass back the nextCursor of the previous page unchanged.");
    }
}
