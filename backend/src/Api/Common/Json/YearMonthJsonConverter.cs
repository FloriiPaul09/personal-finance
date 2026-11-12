using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalFinance.Domain.Budgets;

namespace PersonalFinance.Api.Common.Json;

/// <summary>
/// A month as <c>"2026-06"</c> (ADR-0019): what the user picks, with no day that could be anything but the first.
/// Anything else is unreadable JSON, so <c>400</c> <c>request.invalid</c>.
/// </summary>
internal sealed class YearMonthJsonConverter : JsonConverter<YearMonth>
{
    public override YearMonth Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && YearMonth.TryParse(reader.GetString(), out var month)
            ? month
            : throw new JsonException("A month is written as \"yyyy-MM\", e.g. \"2026-06\".");

    public override void Write(Utf8JsonWriter writer, YearMonth value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
