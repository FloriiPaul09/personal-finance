using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using PersonalFinance.Api.Common.Concurrency;

namespace PersonalFinance.Api.Common.OpenApi;

/// <summary>
/// Versions in the document (ADR-0013, ADR-0029, docs/backend/api-endpoints.md#versions), by convention so new
/// endpoints are covered without code:
/// <list type="bullet">
/// <item>an endpoint that takes <see cref="IfMatch"/> requires the <c>If-Match</c> header and can answer <c>400</c>
/// (malformed tag), <c>412</c> (stale version) and <c>428</c> (no version) with a ProblemDetails;</item>
/// <item>a <c>200</c> / <c>201</c> whose body is one resource with a <c>Version</c> sends it as <c>ETag</c>. Lists
/// carry <c>version</c> per item instead.</item>
/// </list>
/// </summary>
internal sealed class VersionOperationTransformer : IOpenApiOperationTransformer
{
    public const string IfMatchHeader = "If-Match";
    public const string ETagHeader = "ETag";

    /// <summary>A strong entity tag holding the version, e.g. <c>"12345"</c> with the quotes.</summary>
    public const string EntityTagPattern = "^\"\\d+\"$";

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var description = context.Description;

        if (description.ActionDescriptor.EndpointMetadata.OfType<IfMatchRequiredMetadata>().Any())
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = IfMatchHeader,
                In = ParameterLocation.Header,
                Required = true,
                Description = "The version last read, as the ETag the API sent (e.g. \"12345\", with the quotes).",
                Schema = EntityTagSchema(),
            });

            operation.Responses ??= new OpenApiResponses();
            AddProblem(operation.Responses, "400", "The request is invalid, e.g. a malformed If-Match header (concurrency.version_invalid).");
            AddProblem(operation.Responses, "412", "The resource changed since it was read (concurrency.version_mismatch): nothing was changed. Read it again.");
            AddProblem(operation.Responses, "428", "The If-Match header is missing (concurrency.version_required).");
        }

        foreach (var responseType in description.SupportedResponseTypes)
        {
            if (responseType.StatusCode is not (StatusCodes.Status200OK or StatusCodes.Status201Created)
                || !HasVersion(responseType.Type)
                || operation.Responses?.GetValueOrDefault(responseType.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture)) is not OpenApiResponse response)
            {
                continue;
            }

            response.Headers ??= new Dictionary<string, IOpenApiHeader>();
            response.Headers[ETagHeader] = new OpenApiHeader
            {
                Required = true,
                Description = "The version of the resource, to send back in If-Match.",
                Schema = EntityTagSchema(),
            };
        }

        return Task.CompletedTask;
    }

    /// <summary>One resource with a version: lists and envelopes have no <c>Version</c> of their own.</summary>
    internal static bool HasVersion(Type? type) =>
        type?.GetProperty("Version")?.PropertyType == typeof(uint);

    private static OpenApiSchema EntityTagSchema() => new()
    {
        Type = JsonSchemaType.String,
        Pattern = EntityTagPattern,
        Example = JsonValue.Create("\"12345\""),
    };

    private static void AddProblem(OpenApiResponses responses, string status, string text) =>
        responses.TryAdd(status, new OpenApiResponse
        {
            Description = text,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [ProblemDetailsSchema.ContentType] = new() { Schema = new OpenApiSchemaReference(ProblemDetailsSchema.SchemaId) },
            },
        });
}
