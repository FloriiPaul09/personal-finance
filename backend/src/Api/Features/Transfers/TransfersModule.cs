using PersonalFinance.Api.Common.Endpoints;

namespace PersonalFinance.Api.Features.Transfers;

/// <summary>Money moved between two accounts of the current user: <c>/api/transfers</c> (ADR-0018, docs/backend/api-endpoints.md#transfers).</summary>
internal sealed class TransfersModule : ApiModule
{
    /// <summary>Path of the collection, for <c>Location</c> headers: <c>/api/transfers/{id}</c>.</summary>
    internal const string Path = Common.Endpoints.EndpointRouteBuilderExtensions.ApiPrefix + "/transfers";

    public override string Prefix => "transfers";

    public override string Tag => "Transfers";
}
