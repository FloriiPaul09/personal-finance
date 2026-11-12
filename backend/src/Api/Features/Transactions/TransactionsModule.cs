using PersonalFinance.Api.Common.Endpoints;

namespace PersonalFinance.Api.Features.Transactions;

/// <summary>Income, expenses and transfer legs of the current user: <c>/api/transactions</c> (ADR-0017, docs/backend/api-endpoints.md#transactions).</summary>
internal sealed class TransactionsModule : ApiModule
{
    /// <summary>Path of the collection, for <c>Location</c> headers: <c>/api/transactions/{id}</c>.</summary>
    internal const string Path = Common.Endpoints.EndpointRouteBuilderExtensions.ApiPrefix + "/transactions";

    public override string Prefix => "transactions";

    public override string Tag => "Transactions";
}
