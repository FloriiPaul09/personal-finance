using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PersonalFinance.Testing;

namespace PersonalFinance.Api.Tests.Support;

/// <summary>
/// The real API (<c>Program.cs</c>, same services and pipeline) on a test database (docs/backend/testing.md#hosts),
/// plus <see cref="TestAuthenticationHandler"/>.
/// </summary>
/// <remarks>
/// Test settings are added as the last configuration source, so they win over <c>appsettings.*.json</c> and environment
/// variables: tests never touch the development database. <paramref name="settings"/> overrides them; an empty value
/// removes one (e.g. the initial user, for startup tests).
/// </remarks>
public sealed class ApiFactory(
    TestDatabase database,
    string environment,
    IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    public const string InitialUserEmail = "owner@example.com";
    public const string InitialUserPassword = "initial-user-password";

    public ApiFactory(TestDatabase database)
        : this(database, Environments.Development)
    {
    }

    /// <summary>A client that behaves like the browser app; signed in as <paramref name="userId"/> when given.</summary>
    public HttpClient CreateApiClient(Guid? userId = null) => TestClients.Create(Server.CreateHandler(), userId);

    /// <summary>A client on plain HTTP, like local development on <c>http://localhost</c>.</summary>
    public HttpClient CreatePlainHttpClient() => TestClients.Create(Server.CreateHandler(), baseAddress: new Uri("http://localhost"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = database.ConnectionString,
                ["Auth:InitialUser:Email"] = InitialUserEmail,
                ["Auth:InitialUser:Password"] = InitialUserPassword,

                // Every test client shares one address: the real limit is tested on its own host.
                ["Auth:LoginRateLimit:PermitLimit"] = "100000",

                // SQL of every test drowns the output of a failing one.
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning",
            };

            foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
            {
                values[key] = value;
            }

            configuration.AddInMemoryCollection(values);
        });
        builder.ConfigureTestServices(TestAuthenticationHandler.AddTo);
    }
}
