namespace PersonalFinance.Api.Common.Endpoints;

/// <summary>
/// A module of the API: one route group <c>/api/{Prefix}</c> with one OpenAPI tag, shared by all its endpoints
/// (ADR-0003, ADR-0012). One sealed subclass per folder of <c>Features</c>, e.g. <c>AccountsModule</c>.
/// </summary>
/// <remarks>
/// Authorization is not set here: every endpoint requires a signed-in user through the fallback policy, and only
/// the few public endpoints opt out with <c>AllowAnonymous()</c> (docs/backend/security.md).
/// </remarks>
public abstract class ApiModule
{
    /// <summary>Route segment after <c>/api</c>: lowercase words separated by hyphens, e.g. <c>accounts</c>.</summary>
    public abstract string Prefix { get; }

    /// <summary>OpenAPI tag of every endpoint of the module, e.g. <c>Accounts</c>.</summary>
    public abstract string Tag { get; }

    /// <summary>Conventions applied to the whole module (e.g. a rate limiter). Nothing by default.</summary>
    public virtual void Configure(RouteGroupBuilder group)
    {
    }
}
