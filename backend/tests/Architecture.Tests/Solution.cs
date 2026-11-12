using ArchUnitNET.Loader;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api;
using PersonalFinance.Domain;
using PersonalFinance.Infrastructure;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace PersonalFinance.Architecture.Tests;

/// <summary>
/// The backend loaded once for every rule (ArchUnitNET reads the compiled assemblies). EF Core is loaded too, so
/// rules can name its methods; this test assembly is loaded so the rules can be checked against known violations.
/// </summary>
internal static class Solution
{
    public static readonly ArchitectureModel Model = new ArchLoader()
        .LoadAssemblies(
            ApiAssembly.Reference,
            DomainAssembly.Reference,
            InfrastructureAssembly.Reference,
            typeof(EntityFrameworkQueryableExtensions).Assembly,
            typeof(Solution).Assembly)
        .Build();
}
