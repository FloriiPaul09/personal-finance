using PersonalFinance.Api.Common.Endpoints;

namespace PersonalFinance.Api.Features.Accounts;

/// <summary>Accounts of the current user: <c>/api/accounts</c> (ADR-0015, docs/backend/api-endpoints.md#accounts).</summary>
internal sealed class AccountsModule : ApiModule
{
    public override string Prefix => "accounts";

    public override string Tag => "Accounts";
}
