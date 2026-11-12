using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalFinance.Api.Common.Json;

/// <summary>
/// JSON of every request and response (ADR-0015, docs/backend/api-endpoints.md#conventions): ASP.NET Core web
/// defaults (<c>camelCase</c> properties, case-insensitive reading) plus enumerations as <c>camelCase</c> strings and
/// months as <c>"yyyy-MM"</c>.
/// </summary>
public static class JsonConventions
{
    /// <summary>The options of the API, for clients written in .NET (tests included).</summary>
    public static JsonSerializerOptions Options { get; } = Apply(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static IServiceCollection AddApiJson(this IServiceCollection services) =>
        services.ConfigureHttpJsonOptions(options => Apply(options.SerializerOptions));

    private static JsonSerializerOptions Apply(JsonSerializerOptions options)
    {
        // "creditCard", never 2: reordering or adding members never changes the contract. Numbers are refused, so a
        // client cannot depend on the order of the members either.
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

        // Months as "2026-06" (ADR-0019).
        options.Converters.Add(new YearMonthJsonConverter());
        return options;
    }
}
