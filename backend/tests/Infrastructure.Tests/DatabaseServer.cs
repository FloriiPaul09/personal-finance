using PersonalFinance.Infrastructure.Tests;
using PersonalFinance.Testing;

[assembly: AssemblyFixture(typeof(DatabaseServer))]

namespace PersonalFinance.Infrastructure.Tests;

/// <summary>
/// The PostgreSQL server of this test run (ADR-0011): one container, a template database with every migration applied,
/// one database per test class.
/// </summary>
public sealed class DatabaseServer : PostgresServerFixture
{
}
