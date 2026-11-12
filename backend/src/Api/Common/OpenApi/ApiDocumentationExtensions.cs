using Scalar.AspNetCore;

namespace PersonalFinance.Api.Common.OpenApi;

public static class ApiDocumentationExtensions
{
    /// <summary>The built-in OpenAPI document generator (ADR-0003); the document is served in development only.</summary>
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services) => services.AddOpenApi();

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
