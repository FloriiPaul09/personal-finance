using PersonalFinance.Testing;

namespace PersonalFinance.Api.Tests.Support;

/// <summary>
/// Base class of endpoint tests: the database of the test class (ADR-0011) and the API host running on it.
/// Every test creates its own users and never asserts on counts across the whole database.
/// </summary>
public abstract class ApiTest(ApiServer server) : DatabaseTest(server)
{
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The Development host of this test class, started on first use.</summary>
    protected ApiFactory Host => ((ApiServer)Server).HostForClass(GetType(), Database);

    /// <summary>A client without session: every request is anonymous.</summary>
    protected HttpClient AnonymousClient() => Host.CreateClient();
}
