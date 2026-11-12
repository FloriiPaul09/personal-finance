using PersonalFinance.Api.Common.Security;

namespace PersonalFinance.Api.Tests.Support;

/// <summary>
/// Does what Angular does (docs/backend/security.md#csrf): fetches the antiforgery token before the first unsafe
/// request, sends it in <c>X-XSRF-TOKEN</c>, and keeps the latest <c>XSRF-TOKEN</c> cookie the API sets (login and
/// logout issue a new one). Must sit outside the cookie handler, so the token request stores the antiforgery cookie.
/// </summary>
public sealed class AntiforgeryHandler : DelegatingHandler
{
    private const string TokenPath = "/api/auth/antiforgery";

    private string? _token;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsUnsafe(request.Method) && !request.Headers.Contains(AntiforgeryCookie.HeaderName))
        {
            _token ??= await FetchTokenAsync(request, cancellationToken);
            request.Headers.Add(AntiforgeryCookie.HeaderName, _token);
        }

        var response = await base.SendAsync(request, cancellationToken);
        Remember(response);
        return response;
    }

    private async Task<string> FetchTokenAsync(HttpRequestMessage original, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(original.RequestUri!, TokenPath));

        // The token is bound to the user: ask for it as the same user.
        if (original.Headers.TryGetValues(TestAuthenticationHandler.Header, out var user))
        {
            request.Headers.Add(TestAuthenticationHandler.Header, user);
        }

        using var response = await base.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        Remember(response);
        return _token ?? throw new InvalidOperationException($"{TokenPath} did not set {AntiforgeryCookie.CookieName}.");
    }

    private void Remember(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return;
        }

        var prefix = AntiforgeryCookie.CookieName + "=";
        foreach (var cookie in cookies.Where(cookie => cookie.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var end = cookie.IndexOf(';', StringComparison.Ordinal);
            _token = Uri.UnescapeDataString(end < 0 ? cookie[prefix.Length..] : cookie[prefix.Length..end]);
        }
    }

    private static bool IsUnsafe(HttpMethod method) =>
        method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch || method == HttpMethod.Delete;
}
