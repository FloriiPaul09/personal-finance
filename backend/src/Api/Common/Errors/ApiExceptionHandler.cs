using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PersonalFinance.Domain.Common;

namespace PersonalFinance.Api.Common.Errors;

/// <summary>
/// Turns the exceptions that describe a client error into ProblemDetails responses (ADR-0013,
/// docs/backend/validation-and-errors.md). Anything else is left to the exception handler middleware, which logs it
/// and answers <c>500</c> without details.
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = ToProblem(exception);
        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    /// <summary>The response for <paramref name="exception"/>, or <see langword="null"/> when it is a server error.</summary>
    internal static ProblemDetails? ToProblem(Exception exception) => exception switch
    {
        ApiProblemException api => Problem(api.StatusCode, api.Code, api.Message, api.Field),
        DomainException domain => FromDomain(domain),

        // The row changed (or disappeared) between the read and the write: same answer as a stale If-Match.
        DbUpdateConcurrencyException => Problem(
            StatusCodes.Status412PreconditionFailed,
            ApiErrors.VersionMismatch,
            ApiProblemException.VersionMismatch().Message),

        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } unique }
            when UniqueConstraintErrors.TryGetCode(unique.ConstraintName, out var code) => Problem(
                StatusCodes.Status409Conflict,
                code,
                "Another item already uses this value."),

        _ => null,
    };

    private static ProblemDetails? FromDomain(DomainException exception)
    {
        var status = DomainErrorStatuses.StatusOf(exception.Code);
        return status >= StatusCodes.Status500InternalServerError
            ? null
            : Problem(status, exception.Code, exception.Message);
    }

    private static ProblemDetails Problem(int status, string code, string detail, string? field = null)
    {
        ProblemDetails problem = field is null
            ? new ProblemDetails()
            : new HttpValidationProblemDetails(new Dictionary<string, string[]> { [field] = [detail] });

        problem.Status = status;
        problem.Detail = detail;
        problem.Extensions[ApiErrorHandling.CodeExtension] = code;
        return problem;
    }
}
