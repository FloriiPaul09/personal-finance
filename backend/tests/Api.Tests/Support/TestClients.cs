using Microsoft.AspNetCore.Mvc.Testing.Handlers;

namespace PersonalFinance.Api.Tests.Support;

/// <summary>HTTP clients that behave like the browser app: HTTPS (cookies are <c>Secure</c>), cookies, antiforgery.</summary>
public static class TestClients
{
    public static readonly Uri BaseAddress = new("https://localhost");

    /// <param name="server">Handler of the test server.</param>
    /// <param name="userId">Signed in through <see cref="TestAuthenticationHandler"/>; <see langword="null"/> for a client without session.</param>
    public static HttpClient Create(HttpMessageHandler server, Guid? userId = null)
    {
        var client = new HttpClient(new AntiforgeryHandler { InnerHandler = new CookieContainerHandler { InnerHandler = server } })
        {
            BaseAddress = BaseAddress,
        };

        if (userId is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.Header, userId.Value.ToString());
        }

        return client;
    }
}
