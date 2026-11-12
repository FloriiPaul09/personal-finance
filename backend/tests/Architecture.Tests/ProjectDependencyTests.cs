using ArchUnitNET.xUnitV3;
using PersonalFinance.Api;
using PersonalFinance.Domain;
using PersonalFinance.Infrastructure;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace PersonalFinance.Architecture.Tests;

// Dependency rules between projects (ADR-0002, docs/architecture/backend-structure.md#dependencies).
public sealed class ProjectDependencyTests
{
    [Fact]
    public void Domain_DependsOnNoOtherProjectAndNoFramework()
    {
        Types().That().ResideInAssembly(DomainAssembly.Reference)
            .Should().NotDependOnAny(Types(includeReferenced: true).That().ResideInNamespaceMatching(
                @"^(PersonalFinance\.(Api|Infrastructure)|Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore|Npgsql)(\.|$)"))
            .Because("business rules stay free of persistence and HTTP (ADR-0002)")
            .Check(Solution.Model);
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApi()
    {
        Types().That().ResideInAssembly(InfrastructureAssembly.Reference)
            .Should().NotDependOnAny(Types().That().ResideInAssembly(ApiAssembly.Reference))
            .Because("persistence is reused by other hosts such as the future Worker (ADR-0002)")
            .Check(Solution.Model);
    }
}
