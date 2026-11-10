using PersonalFinance.Infrastructure.Tests;
using PersonalFinance.Testing;

[assembly: AssemblyFixture(typeof(DatabaseServer))]

namespace PersonalFinance.Infrastructure.Tests;

/// <summary>
/// The PostgreSQL server of this test run (ADR-0011). From the persistence branch on, it applies the
/// migrations to the template database by overriding <see cref="PostgresServerFixture.InitializeTemplateAsync"/>.
/// </summary>
public sealed class DatabaseServer : PostgresServerFixture
{
}
