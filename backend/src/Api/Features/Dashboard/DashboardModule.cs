using PersonalFinance.Api.Common.Endpoints;

namespace PersonalFinance.Api.Features.Dashboard;

/// <summary>Read-only summaries of the current user's money: <c>/api/dashboard</c> (ADR-0020, docs/backend/api-endpoints.md#dashboard).</summary>
internal sealed class DashboardModule : ApiModule
{
    public override string Prefix => "dashboard";

    public override string Tag => "Dashboard";
}
