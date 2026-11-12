using ArchUnitNET.Loader;
using PersonalFinance.Api;
using PersonalFinance.Domain;
using PersonalFinance.Infrastructure;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace PersonalFinance.Architecture.Tests;

/// <summary>
/// The backend loaded once for every rule (ArchUnitNET reads the compiled assemblies). This test assembly is loaded
/// too, so the rules can be checked against known violations.
/// </summary>
internal static class Solution
{
    public static readonly ArchitectureModel Model = new ArchLoader()
        .LoadAssemblies(
            ApiAssembly.Reference,
            DomainAssembly.Reference,
            InfrastructureAssembly.Reference,
            typeof(Solution).Assembly)
        .Build();
}
