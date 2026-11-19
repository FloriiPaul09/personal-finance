using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

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

            if (problem is HttpValidationProblemDetails validation)
            {
                var naming = context.HttpContext.RequestServices.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions.PropertyNamingPolicy
                    ?? JsonNamingPolicy.CamelCase;
                NameFieldsAsJson(validation.Errors, naming);
            }
        });

        services.AddExceptionHandler<ApiExceptionHandler>();
        return services;
    }

    /// <summary>
    /// Keys of <c>errors</c> named as the JSON properties of the request (ADR-0029): built-in validation reports the C#
    /// names (<c>Email</c>, <c>Tags[0]</c>), the client knows <c>email</c>, <c>tags[0]</c>. Each segment of a path is
    /// renamed; messages of keys that end up equal are merged.
    /// </summary>
    internal static void NameFieldsAsJson(IDictionary<string, string[]> errors, JsonNamingPolicy naming)
    {
        if (errors.Count == 0)
        {
            return;
        }

        var renamed = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (key, messages) in errors)
        {
            var name = JsonPath(key, naming);
            renamed[name] = renamed.TryGetValue(name, out var existing) ? [.. existing, .. messages] : messages;
        }

        errors.Clear();
        foreach (var (key, messages) in renamed)
        {
            errors[key] = messages;
        }
    }

    /// <summary><c>Lines[1].AccountId</c> → <c>lines[1].accountId</c>; the indexes stay as they are.</summary>
    internal static string JsonPath(string key, JsonNamingPolicy naming) =>
        string.Join('.', key.Split('.').Select(segment =>
        {
            var index = segment.IndexOf('[', StringComparison.Ordinal);
            return index < 0
                ? naming.ConvertName(segment)
                : naming.ConvertName(segment[..index]) + segment[index..];
        }));

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
