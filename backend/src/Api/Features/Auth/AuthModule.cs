using PersonalFinance.Api.Common.Endpoints;

namespace PersonalFinance.Api.Features.Auth;

/// <summary>Session of the current user: <c>/api/auth</c> (ADR-0014, docs/backend/api-endpoints.md#authentication).</summary>
internal sealed class AuthModule : ApiModule
{
    public override string Prefix => "auth";

    public override string Tag => "Auth";
}
