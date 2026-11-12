using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PersonalFinance.Testing;

namespace PersonalFinance.Api.Tests.Support;

/// <summary>
/// The real API (<c>Program.cs</c>, same services and pipeline) on a test database (docs/backend/testing.md#hosts).
/// </summary>
/// <remarks>
/// The connection string is added as the last configuration source, so it wins over <c>appsettings.*.json</c> and
/// environment variables: tests never touch the development database.
/// </remarks>
public sealed class ApiFactory(TestDatabase database, string environment) : WebApplicationFactory<Program>
{
    public ApiFactory(TestDatabase database)
        : this(database, Environments.Development)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = database.ConnectionString,
            }));
    }
}
