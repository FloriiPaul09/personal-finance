using PersonalFinance.Api.Common.Endpoints;

namespace PersonalFinance.Api.Features.Categories;

/// <summary>Categories of the current user: <c>/api/categories</c> (ADR-0016, docs/backend/api-endpoints.md#categories).</summary>
internal sealed class CategoriesModule : ApiModule
{
    /// <summary>Path of the collection, for <c>Location</c> headers: <c>/api/categories/{id}</c>.</summary>
    internal const string Path = Common.Endpoints.EndpointRouteBuilderExtensions.ApiPrefix + "/categories";

    public override string Prefix => "categories";

    public override string Tag => "Categories";
}
