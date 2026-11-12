namespace PersonalFinance.Api.Common.Errors;

/// <summary>
/// Stable codes of errors raised by the API itself, in the <c>code</c> property of every error response
/// (docs/backend/validation-and-errors.md#codes). Business rule codes come from <c>DomainErrors</c>.
/// </summary>
public static class ApiErrors
{
    public const string ValidationFailed = "validation.failed";
    public const string RequestInvalid = "request.invalid";
    public const string MethodNotAllowed = "request.method_not_allowed";
    public const string UnsupportedMediaType = "request.unsupported_media_type";
    public const string TooManyRequests = "request.too_many";

    public const string AuthenticationRequired = "auth.required";
    public const string Forbidden = "auth.forbidden";
    public const string AntiforgeryInvalid = "auth.antiforgery_invalid";
    public const string InvalidCredentials = "auth.invalid_credentials";
    public const string PasswordIncorrect = "auth.password_incorrect";
    public const string PasswordRejected = "auth.password_rejected";
    public const string EmailTaken = "auth.email_taken";

    public const string NotFound = "resource.not_found";
    public const string Conflict = "resource.conflict";

    public const string VersionRequired = "concurrency.version_required";
    public const string VersionInvalid = "concurrency.version_invalid";
    public const string VersionMismatch = "concurrency.version_mismatch";

    public const string AccountNameTaken = "account.name_taken";
    public const string CategoryNameTaken = "category.name_taken";
    public const string TagNameTaken = "tag.name_taken";
    public const string BudgetMonthTaken = "budget.month_taken";

    public const string ServerError = "server.error";

    /// <summary>
    /// An id in the request body that the user does not own or that does not exist, e.g. <c>account.not_found</c>.
    /// The two cases are indistinguishable on purpose (docs/backend/security.md#another-users-data).
    /// </summary>
    public static string ReferenceNotFound(string resource) => $"{resource}.not_found";

    /// <summary>Code of an error response that has none, from its status.</summary>
    public static string ForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => RequestInvalid,
        StatusCodes.Status401Unauthorized => AuthenticationRequired,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
        StatusCodes.Status409Conflict => Conflict,
        StatusCodes.Status412PreconditionFailed => VersionMismatch,
        StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaType,
        StatusCodes.Status428PreconditionRequired => VersionRequired,
        StatusCodes.Status429TooManyRequests => TooManyRequests,
        >= 500 => ServerError,
        _ => $"http.{statusCode}",
    };
}
