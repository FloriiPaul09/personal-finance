using ArchUnitNET.Domain.Extensions;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnitV3;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Infrastructure.Persistence;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace PersonalFinance.Architecture.Tests;

// Rules of the Api project (ADR-0003, ADR-0012, docs/backend/README.md#endpoints).
public sealed class ApiRulesTests
{
    private const string FeaturesNamespace = @"^PersonalFinance\.Api\.Features(\.|$)";

    [Fact]
    public void Endpoints_AreSealedInternalClassesOfAFeature()
    {
        Classes().That().AreAssignableTo(typeof(IEndpoint)).And().ResideInAssembly(ApiAssembly.Reference)
            .Should().BeSealed()
            .AndShould().NotBePublic()
            .AndShould().ResideInNamespaceMatching(FeaturesNamespace)
            .Because("an endpoint lives in its slice and is reached only through HTTP (ADR-0003)")
            .WithoutRequiringPositiveResults()
            .Check(Solution.Model);
    }

    [Fact]
    public void Modules_AreSealedClassesOfAFeature()
    {
        Classes().That().AreAssignableTo(typeof(ApiModule)).And().AreNotAbstract().And().ResideInAssembly(ApiAssembly.Reference)
            .Should().BeSealed()
            .AndShould().ResideInNamespaceMatching(FeaturesNamespace)
            .Because("each folder of Features is one module (ADR-0012)")
            .WithoutRequiringPositiveResults()
            .Check(Solution.Model);
    }

    [Fact]
    public void Api_NeverBypassesTheUserScopeFilter()
    {
        NoCallToIgnoreQueryFilters(Types().That().ResideInAssembly(ApiAssembly.Reference))
            .Check(Solution.Model);
    }

    [Fact]
    public void IgnoreQueryFiltersRule_DetectsACall()
    {
        // Guards the rule above: if ArchUnitNET stopped seeing calls into EF Core, it would pass on anything.
        var rule = NoCallToIgnoreQueryFilters(Types().That().HaveFullName(typeof(BypassesUserScope).FullName!));

        Assert.False(rule.HasNoViolations(Solution.Model));
    }

    // Matched by declaring type and name, not with NotCallAny(MethodMembers()...): a call to a generic method such as
    // IgnoreQueryFilters<Account> is recorded with the signature seen at the call site, which never equals the method
    // member loaded from EF Core, so NotCallAny would never see it.
    private static IArchRule NoCallToIgnoreQueryFilters(ArchUnitNET.Fluent.Syntax.Elements.Types.GivenTypesConjunction types) =>
        types.Should().FollowCustomCondition(
                type => !type.GetCalledMethods().Any(method =>
                    method.DeclaringType.FullName == typeof(EntityFrameworkQueryableExtensions).FullName
                    && method.Name.StartsWith(nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters), StringComparison.Ordinal)),
                "not call IgnoreQueryFilters",
                "calls IgnoreQueryFilters")
            .Because("slices never read or write data of other users (docs/database/user-isolation.md)");

    /// <summary>A known violation, never called: the Api project bans this call at compile time (BannedSymbols.txt).</summary>
    private static class BypassesUserScope
    {
        public static IQueryable<Account> AllAccounts(AppDbContext db) => db.Accounts.IgnoreQueryFilters();
    }
}
