using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using PersonalFinance.Domain.Budgets;

namespace PersonalFinance.Api.Common.OpenApi;

/// <summary>
/// Describes <see cref="YearMonth"/> as the <c>"yyyy-MM"</c> string the API reads and writes (ADR-0019, ADR-0029).
/// Its JSON converter is invisible to the document generator, which would otherwise publish an empty schema.
/// </summary>
internal sealed class YearMonthSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>Name of the schema component, the one query parameters with <see cref="MonthQueryAttribute"/> point to.</summary>
    public const string SchemaId = nameof(YearMonth);

    /// <summary>Four digits, a hyphen, a month from 01 to 12.</summary>
    public const string Pattern = @"^\d{4}-(0[1-9]|1[0-2])$";

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if ((Nullable.GetUnderlyingType(type) ?? type) != typeof(YearMonth))
        {
            return Task.CompletedTask;
        }

        var nullable = schema.Type is { } current && current.HasFlag(JsonSchemaType.Null);
        schema.Type = nullable ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
        schema.Pattern = Pattern;
        schema.Description = "A month, written yyyy-MM.";
        schema.Example = JsonValue.Create("2026-06");
        return Task.CompletedTask;
    }
}
