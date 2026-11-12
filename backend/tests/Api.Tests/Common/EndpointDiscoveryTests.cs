using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using PersonalFinance.Api.Common.Endpoints;

namespace PersonalFinance.Api.Tests.Common;

// How endpoints are found and grouped by module (docs/backend/README.md#endpoints). Every mistake must stop startup.
public sealed class EndpointDiscoveryTests
{
    [Fact]
    public void Endpoints_AreGroupedByTheirModule_InPrefixOrder()
    {
        var modules = EndpointDiscovery.FromTypes(
            [typeof(ZooModule), typeof(ListZoo), typeof(BooksModule), typeof(ListBooks), typeof(CreateBook)]);

        Assert.Equal(new[] { "books", "zoo" }, modules.Select(module => module.Module.Prefix));
        Assert.Equal(new[] { typeof(CreateBook), typeof(ListBooks) }, modules[0].Endpoints.Select(endpoint => endpoint.GetType()));
        Assert.Equal(new[] { typeof(ListZoo) }, modules[1].Endpoints.Select(endpoint => endpoint.GetType()));
    }

    [Fact]
    public void ModuleUsedOnlyByEndpoints_IsFound()
    {
        var module = Assert.Single(EndpointDiscovery.FromTypes([typeof(ListBooks)]));

        Assert.IsType<BooksModule>(module.Module);
    }

    [Fact]
    public void ModuleWithoutEndpoints_IsKeptEmpty()
    {
        var module = Assert.Single(EndpointDiscovery.FromTypes([typeof(ZooModule)]));

        Assert.Empty(module.Endpoints);
    }

    [Fact]
    public void AbstractTypes_AreIgnored()
    {
        Assert.Empty(EndpointDiscovery.FromTypes([typeof(ApiModule), typeof(AbstractEndpoint)]));
    }

    [Fact]
    public void EndpointWithoutModule_StopsStartup()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => EndpointDiscovery.FromTypes([typeof(EndpointWithoutModule)]));

        Assert.Contains(nameof(EndpointWithoutModule), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EndpointInTwoModules_StopsStartup()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => EndpointDiscovery.FromTypes([typeof(EndpointInTwoModules)]));

        Assert.Contains(nameof(EndpointInTwoModules), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoModulesWithTheSamePrefix_StopStartup()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => EndpointDiscovery.FromTypes([typeof(BooksModule), typeof(OtherBooksModule)]));

        Assert.Contains("'books'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(UpperCasePrefixModule))]
    [InlineData(typeof(SlashPrefixModule))]
    [InlineData(typeof(UnderscorePrefixModule))]
    [InlineData(typeof(EmptyPrefixModule))]
    [InlineData(typeof(NoTagModule))]
    public void InvalidModule_StopsStartup(Type moduleType)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => EndpointDiscovery.FromTypes([moduleType]));

        Assert.Contains(moduleType.Name, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiAssembly_IsDiscoveredWithoutErrors_AndEveryModuleHasEndpoints()
    {
        var modules = EndpointDiscovery.FromAssembly(ApiAssembly.Reference);

        Assert.All(modules, module => Assert.NotEmpty(module.Endpoints));
    }

    private sealed class BooksModule : ApiModule
    {
        public override string Prefix => "books";

        public override string Tag => "Books";
    }

    private sealed class OtherBooksModule : ApiModule
    {
        public override string Prefix => "books";

        public override string Tag => "Other books";
    }

    private sealed class ZooModule : ApiModule
    {
        public override string Prefix => "zoo";

        public override string Tag => "Zoo";
    }

    private sealed class UpperCasePrefixModule : ApiModule
    {
        public override string Prefix => "Books";

        public override string Tag => "Books";
    }

    private sealed class SlashPrefixModule : ApiModule
    {
        public override string Prefix => "/books";

        public override string Tag => "Books";
    }

    private sealed class UnderscorePrefixModule : ApiModule
    {
        public override string Prefix => "book_list";

        public override string Tag => "Books";
    }

    private sealed class EmptyPrefixModule : ApiModule
    {
        public override string Prefix => "";

        public override string Tag => "Books";
    }

    private sealed class NoTagModule : ApiModule
    {
        public override string Prefix => "no-tag";

        public override string Tag => " ";
    }

    private sealed class ListBooks : IEndpoint<BooksModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("", () => "books");
    }

    private sealed class CreateBook : IEndpoint<BooksModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPost("", () => "created");
    }

    private sealed class ListZoo : IEndpoint<ZooModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("", () => "zoo");
    }

    private abstract class AbstractEndpoint : IEndpoint<BooksModule>
    {
        public abstract void Map(IEndpointRouteBuilder module);
    }

    private sealed class EndpointWithoutModule : IEndpoint
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("", () => "none");
    }

    private sealed class EndpointInTwoModules : IEndpoint<BooksModule>, IEndpoint<ZooModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("", () => "both");
    }
}
