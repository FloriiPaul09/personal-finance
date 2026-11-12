using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Domain.Common;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Common.Concurrency;

public static class VersionExtensions
{
    /// <summary>
    /// Refuses the change with <c>412</c> when the client did not read the current version of
    /// <paramref name="entity"/>. A change made after this check and before <c>SaveChanges</c> is caught by EF Core
    /// (<c>xmin</c> concurrency token) and answered the same way.
    /// </summary>
    public static void EnsureVersion(this AppDbContext db, OwnedEntity entity, IfMatch expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        if (RowVersion.Of(db, entity) != expected.Version)
        {
            throw ApiProblemException.VersionMismatch();
        }
    }

    /// <summary>Sends the version of a single resource as <c>ETag</c>, on reads and after writes.</summary>
    public static void SetVersion(this HttpResponse response, uint version) =>
        response.Headers.ETag = EntityTag.Format(version);
}
