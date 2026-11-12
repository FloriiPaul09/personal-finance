namespace PersonalFinance.Api.Common.Endpoints;

/// <summary>
/// An HTTP endpoint, defined in its slice. Never implemented directly: use <see cref="IEndpoint{TModule}"/>, which
/// says which module the endpoint belongs to.
/// </summary>
public interface IEndpoint
{
    /// <summary>
    /// Maps the endpoint on the group of its module, which already has the prefix <c>/api/{module}</c> and the tag.
    /// Use <c>""</c> for the collection itself and <c>"{id:guid}"</c> for one item.
    /// </summary>
    void Map(IEndpointRouteBuilder module);
}

/// <summary>
/// An endpoint of the module <typeparamref name="TModule"/>, discovered at startup (docs/backend/README.md#endpoints).
/// </summary>
/// <example>
/// <code>
/// internal sealed class Endpoint : IEndpoint&lt;AccountsModule&gt;
/// {
///     public void Map(IEndpointRouteBuilder module) =&gt; module.MapPost("", Handle).WithName(nameof(CreateAccount));
/// }
/// </code>
/// </example>
public interface IEndpoint<TModule> : IEndpoint
    where TModule : ApiModule
{
}
