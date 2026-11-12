using PersonalFinance.Api.Common.Endpoints;

namespace PersonalFinance.Api.Features.Budgets;

/// <summary>Monthly limits of expense categories: <c>/api/budgets</c> (ADR-0019, docs/backend/api-endpoints.md#budgets).</summary>
internal sealed class BudgetsModule : ApiModule
{
    /// <summary>Path of the collection, for <c>Location</c> headers: <c>/api/budgets/{id}</c>.</summary>
    internal const string Path = Common.Endpoints.EndpointRouteBuilderExtensions.ApiPrefix + "/budgets";

    public override string Prefix => "budgets";

    public override string Tag => "Budgets";
}
