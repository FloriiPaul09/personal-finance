using System.Diagnostics;

namespace PersonalFinance.Api.Common.Errors;

public static class ApiErrorHandling
{
    /// <summary>Name of the extension holding the stable error code.</summary>
    public const string CodeExtension = "code";

    /// <summary>Name of the extension holding the id to quote when reporting a problem.</summary>
    public const string TraceIdExtension = "traceId";

    /// <summary>
    /// Error responses of the API (ADR-0013, docs/backend/validation-and-errors.md#response-shape): ProblemDetails
    /// everywhere, with a stable <c>code</c> and a <c>traceId</c>; known exceptions mapped by <see cref="ApiExceptionHandler"/>.
    /// </summary>
    public static IServiceCollection AddApiErrors(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            var status = problem.Status ?? context.HttpContext.Response.StatusCode;

            problem.Extensions.TryAdd(CodeExtension, problem is HttpValidationProblemDetails ? ApiErrors.ValidationFailed : ApiErrors.ForStatus(status));
            problem.Extensions.TryAdd(TraceIdExtension, Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        });

        services.AddExceptionHandler<ApiExceptionHandler>();
        return services;
    }

    /// <summary>
    /// Exception handler and status code pages, before authentication so that a <c>401</c> gets a body too. Responses
    /// without a body (e.g. <c>TypedResults.NotFound()</c>) become ProblemDetails with the code of their status.
    /// </summary>
    public static IApplicationBuilder UseApiErrors(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }
}
