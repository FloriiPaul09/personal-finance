using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PersonalFinance.Api.Common.OpenApi;

/// <summary>Query parameters marked with <see cref="MonthQueryAttribute"/> point to the <c>YearMonth</c> schema (ADR-0029).</summary>
internal sealed class MonthQueryOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        // Minimal API endpoints carry their handler method as metadata.
        var handler = context.Description.ActionDescriptor.EndpointMetadata.OfType<MethodInfo>().FirstOrDefault();
        if (handler is null || operation.Parameters is null)
        {
            return Task.CompletedTask;
        }

        var months = handler.GetParameters()
            .Where(parameter => parameter.IsDefined(typeof(MonthQueryAttribute), inherit: false))
            .Select(parameter => parameter.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>())
        {
            if (parameter.In == ParameterLocation.Query && parameter.Name is not null && months.Contains(parameter.Name))
            {
                parameter.Schema = new OpenApiSchemaReference(YearMonthSchemaTransformer.SchemaId);
            }
        }

        return Task.CompletedTask;
    }
}
