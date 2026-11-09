using System.Reflection;
using PersonalFinance.Domain;
using PersonalFinance.Infrastructure;

namespace PersonalFinance.Architecture.Tests;

// Dependency rules of ADR-0002, checked with plain reflection.
// To be rewritten with the architecture-test library chosen in [Backend].
public class ProjectDependencyTests
{
    [Fact]
    public void Domain_DoesNotReferenceOtherProjectsOrFrameworks()
    {
        var forbidden = References(DomainAssembly.Reference)
            .Where(name => name.StartsWith("PersonalFinance.", StringComparison.Ordinal)
                        || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                        || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                        || name.StartsWith("Npgsql", StringComparison.Ordinal));

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceApi()
    {
        Assert.DoesNotContain("PersonalFinance.Api", References(InfrastructureAssembly.Reference));
    }

    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty);
}
