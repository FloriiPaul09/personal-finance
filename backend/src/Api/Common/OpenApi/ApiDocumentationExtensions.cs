using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Scalar.AspNetCore;

namespace PersonalFinance.Api.Common.OpenApi;

public static class ApiDocumentationExtensions
{
    /// <summary>
    /// <see langword="true"/> while the build writes the document (<c>-p:OpenApiGenerateDocuments=true</c>, ADR-0029):
    /// the tool runs the app's startup without a server, so startup work that needs the database is skipped.
    /// </summary>
    public static bool IsGeneratingDocument { get; } =
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    /// <summary>
    /// The built-in OpenAPI document generator (ADR-0003); the document is served in development only and written at
    /// build time on request. Transformers describe what the generator cannot see (ADR-0029): months, versions and
    /// the error shape.
    /// </summary>
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.CreateSchemaReferenceId = SchemaId;
            options.AddSchemaTransformer<YearMonthSchemaTransformer>();
            options.AddOperationTransformer<MonthQueryOperationTransformer>();
            options.AddOperationTransformer<VersionOperationTransformer>();
            options.AddDocumentTransformer<ProblemDetailsSchema>();
        });

    /// <summary>
    /// Schema name of a type. Slices nest their contracts (<c>Login.Request</c>, <c>ChangePassword.Request</c>): the
    /// declaring type is prepended (<c>LoginRequest</c>), so every schema has its own name and a generated client gets
    /// readable types (ADR-0015).
    /// </summary>
    internal static string? SchemaId(JsonTypeInfo typeInfo)
    {
        var id = OpenApiOptions.CreateDefaultSchemaReferenceId(typeInfo);
        var declaringType = typeInfo.Type.DeclaringType;
        return id is null || declaringType is null ? id : declaringType.Name + id;
    }

    /// <summary>
    /// In development only: the OpenAPI document at <c>/openapi/v1.json</c> and the Scalar UI at <c>/scalar</c>
    /// (ADR-0012). Both are public on a development machine; requests sent from the UI carry the session cookie.
    /// Other environments map nothing, so the routes do not exist.
    /// </summary>
    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference().AllowAnonymous();

        return app;
    }
}
