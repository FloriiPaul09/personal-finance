using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using PersonalFinance.Api.Common.Errors;

namespace PersonalFinance.Api.Common.OpenApi;

/// <summary>
/// The shape of every error (ADR-0013, docs/backend/validation-and-errors.md#response-shape) as one schema component,
/// referenced by the error responses the document describes (ADR-0029). Also removes <c>servers</c>, so the document
/// written at build time and the one served in Development are the same.
/// </summary>
internal sealed class ProblemDetailsSchema : IOpenApiDocumentTransformer
{
    public const string SchemaId = "ProblemDetails";
    public const string ContentType = "application/problem+json";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Servers?.Clear();

        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        document.Components.Schemas[SchemaId] = Create();
        return Task.CompletedTask;
    }

    private static OpenApiSchema Create() => new()
    {
        Type = JsonSchemaType.Object,
        Description = "An error (RFC 9457) with the stable code the client switches on.",
        Required = new HashSet<string> { "status", ApiErrorHandling.CodeExtension, ApiErrorHandling.TraceIdExtension },
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["type"] = Text(null),
            ["title"] = Text(null),
            ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
            ["detail"] = Text("English message for developers; never shown to users as is."),
            ["instance"] = Text(null),
            [ApiErrorHandling.CodeExtension] = Text("Stable error code, e.g. concurrency.version_mismatch."),
            [ApiErrorHandling.TraceIdExtension] = Text("Id to quote when reporting the problem."),
            ["errors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Description = "Messages by request field, named as the JSON property (camelCase).",
                AdditionalProperties = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String },
                },
            },
        },
    };

    private static OpenApiSchema Text(string? description) => new() { Type = JsonSchemaType.String, Description = description };
}
