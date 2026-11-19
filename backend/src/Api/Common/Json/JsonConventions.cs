using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalFinance.Api.Common.Json;

/// <summary>
/// JSON of every request and response (ADR-0015, ADR-0029, docs/backend/api-endpoints.md#conventions): ASP.NET Core
/// web defaults (<c>camelCase</c> properties, case-insensitive reading) plus numbers only as JSON numbers, enumerations
/// as <c>camelCase</c> strings and months as <c>"yyyy-MM"</c>.
/// </summary>
public static class JsonConventions
{
    /// <summary>The options of the API, for clients written in .NET (tests included).</summary>
    public static JsonSerializerOptions Options { get; } = Apply(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static IServiceCollection AddApiJson(this IServiceCollection services) =>
        services.ConfigureHttpJsonOptions(options => Apply(options.SerializerOptions));

    private static JsonSerializerOptions Apply(JsonSerializerOptions options)
    {
        // 12.5, never "12.5" (ADR-0029): the web defaults also read numbers written as text, and the OpenAPI document
        // then describes every number as "number | string". A number in quotes is unreadable JSON → 400 request.invalid.
        options.NumberHandling = JsonNumberHandling.Strict;

        // "creditCard", never 2: reordering or adding members never changes the contract. Numbers are refused, so a
        // client cannot depend on the order of the members either.
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

        // Months as "2026-06" (ADR-0019).
        options.Converters.Add(new YearMonthJsonConverter());
        return options;
    }
}
