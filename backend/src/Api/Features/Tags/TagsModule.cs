using PersonalFinance.Api.Common.Endpoints;

namespace PersonalFinance.Api.Features.Tags;

/// <summary>
/// Tags of the current user: <c>/api/tags</c>, part of the transactions module in commits and docs (ADR-0017,
/// docs/backend/api-endpoints.md#tags). Tags are created by naming them in a transaction.
/// </summary>
internal sealed class TagsModule : ApiModule
{
    public override string Prefix => "tags";

    public override string Tag => "Tags";
}
