using PersonalFinance.Domain.Common;

namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>
/// The version of a row: PostgreSQL's <c>xmin</c>, mapped as the shadow concurrency token of every owned entity
/// (docs/database/conventions.md#concurrency). The API exposes it as <c>ETag</c> and checks it with <c>If-Match</c>.
/// </summary>
public static class RowVersion
{
    /// <summary>Name of the shadow property, for projections: <c>EF.Property&lt;uint&gt;(entity, RowVersion.PropertyName)</c>.</summary>
    public const string PropertyName = "Version";

    /// <summary>The version of a tracked entity as loaded or, after <c>SaveChanges</c>, as written.</summary>
    public static uint Of(AppDbContext db, OwnedEntity entity)
    {
        ArgumentNullException.ThrowIfNull(db);
        return db.Entry(entity).Property<uint>(PropertyName).CurrentValue;
    }
}
