namespace PersonalFinance.Api.Common.Errors;

/// <summary>
/// An error the API detects itself (missing or stale version, reference to data the user does not own). Thrown so
/// slices stay linear; <see cref="ApiExceptionHandler"/> turns it into a ProblemDetails response.
/// </summary>
public sealed class ApiProblemException(int statusCode, string code, string detail, string? field = null)
    : Exception(detail)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;

    /// <summary>Request field the error is about, reported in <c>errors</c> like a validation error.</summary>
    public string? Field { get; } = field;

    public static ApiProblemException VersionRequired() => new(
        StatusCodes.Status428PreconditionRequired,
        ApiErrors.VersionRequired,
        "Send the version of the resource in the If-Match header (the ETag of the last read).");

    public static ApiProblemException VersionInvalid() => new(
        StatusCodes.Status400BadRequest,
        ApiErrors.VersionInvalid,
        "If-Match must contain exactly one entity tag returned by the API, e.g. \"12345\".");

    public static ApiProblemException VersionMismatch() => new(
        StatusCodes.Status412PreconditionFailed,
        ApiErrors.VersionMismatch,
        "The resource was changed or deleted since it was read. Read it again and retry.");

    /// <param name="resource">Lowercase name of the resource, e.g. <c>account</c>.</param>
    /// <param name="field">Request field holding the id, e.g. <c>accountId</c>.</param>
    public static ApiProblemException ReferenceNotFound(string resource, string field) => new(
        StatusCodes.Status400BadRequest,
        ApiErrors.ReferenceNotFound(resource),
        $"No {resource} with this id.",
        field);
}
