using System.Collections.Concurrent;
using PersonalFinance.Api.Tests.Support;
using PersonalFinance.Testing;

[assembly: AssemblyFixture(typeof(ApiServer))]

namespace PersonalFinance.Api.Tests.Support;

/// <summary>
/// The PostgreSQL server of this test run (ADR-0011) plus one API host per test class, pointed at the database of
/// that class (docs/backend/testing.md#hosts). Hosts are created on first use and disposed at the end of the run.
/// </summary>
public sealed class ApiServer : PostgresServerFixture
{
    private readonly ConcurrentDictionary<Type, Lazy<ApiFactory>> _classHosts = new();

    /// <summary>The Development host shared by all the tests of <paramref name="testClass"/>.</summary>
    public ApiFactory HostForClass(Type testClass, TestDatabase database) =>
        _classHosts.GetOrAdd(testClass, _ => new Lazy<ApiFactory>(() => new ApiFactory(database))).Value;

    protected override async ValueTask DisposeDependentsAsync()
    {
        foreach (var host in _classHosts.Values.Where(host => host.IsValueCreated))
        {
            await host.Value.DisposeAsync();
        }
    }
}
