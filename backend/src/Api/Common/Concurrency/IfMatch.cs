using System.Reflection;
using Microsoft.AspNetCore.Http.Metadata;
using PersonalFinance.Api.Common.Errors;

namespace PersonalFinance.Api.Common.Concurrency;

/// <summary>
/// The version the client last read, from the <c>If-Match</c> header (ADR-0013, docs/backend/api-endpoints.md#versions).
/// A parameter of this type makes the header mandatory: missing → <c>428</c>, malformed → <c>400</c>. Compare it with
/// the loaded entity through <see cref="VersionExtensions.EnsureVersion"/>.
/// </summary>
/// <example>
/// <code>
/// internal static async Task&lt;Ok&lt;Response&gt;&gt; Handle(Guid id, Request request, IfMatch ifMatch, AppDbContext db, HttpResponse response, CancellationToken ct)
/// </code>
/// </example>
public sealed class IfMatch : IEndpointParameterMetadataProvider
{
    private IfMatch(uint version) => Version = version;

    public uint Version { get; }

    /// <summary>Called by Minimal API binding. Throws, so the error has the same shape as every other.</summary>
    public static ValueTask<IfMatch?> BindAsync(HttpContext context)
    {
        var values = context.Request.Headers.IfMatch;
        if (values.Count == 0)
        {
            throw ApiProblemException.VersionRequired();
        }

        if (values.Count > 1 || !EntityTag.TryParse(values[0], out var version))
        {
            throw ApiProblemException.VersionInvalid();
        }

        return ValueTask.FromResult<IfMatch?>(new IfMatch(version));
    }

    /// <summary>Marks the endpoint, so a test can check that every update and delete requires a version.</summary>
    public static void PopulateMetadata(ParameterInfo parameter, EndpointBuilder builder) =>
        builder.Metadata.Add(IfMatchRequiredMetadata.Instance);
}

/// <summary>Endpoint metadata: the endpoint takes an <see cref="IfMatch"/> parameter.</summary>
public sealed class IfMatchRequiredMetadata
{
    public static IfMatchRequiredMetadata Instance { get; } = new();

    private IfMatchRequiredMetadata()
    {
    }
}
