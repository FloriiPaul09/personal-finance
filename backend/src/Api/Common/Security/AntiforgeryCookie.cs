using Microsoft.AspNetCore.Antiforgery;
using PersonalFinance.Api.Common.Errors;

namespace PersonalFinance.Api.Common.Security;

/// <summary>
/// CSRF protection on top of <c>SameSite=Strict</c> (ADR-0014, docs/backend/security.md#csrf), in the form Angular
/// supports out of the box: the token is in the readable cookie <c>XSRF-TOKEN</c> and comes back in the header
/// <c>X-XSRF-TOKEN</c> on every <c>POST</c>, <c>PUT</c>, <c>PATCH</c> and <c>DELETE</c>.
/// </summary>
public static class AntiforgeryCookie
{
    /// <summary>Readable by JavaScript on purpose: Angular copies it into the header.</summary>
    public const string CookieName = "XSRF-TOKEN";

    public const string HeaderName = "X-XSRF-TOKEN";

    /// <summary>
    /// Sends a fresh token. The token is bound to the user of <paramref name="httpContext"/>: call it again whenever
    /// that user changes (login, logout, registration).
    /// </summary>
    public static void Issue(HttpContext httpContext, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        httpContext.Response.Cookies.Append(CookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,

            // Same rule as the antiforgery cookie: Secure on HTTPS (docs/backend/security.md#csrf).
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
        });
    }
}

/// <summary>Validates the antiforgery token of every unsafe request of the API, on the <c>/api</c> group.</summary>
internal sealed class AntiforgeryValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Reads never need a token; only requests that change data are checked.
        if (IsUnsafe(httpContext.Request.Method))
        {
            var antiforgery = httpContext.RequestServices.GetRequiredService<IAntiforgery>();
            if (!await antiforgery.IsRequestValidAsync(httpContext))
            {
                throw ApiProblemException.AntiforgeryInvalid();
            }
        }

        return await next(context);
    }

    private static bool IsUnsafe(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
}
