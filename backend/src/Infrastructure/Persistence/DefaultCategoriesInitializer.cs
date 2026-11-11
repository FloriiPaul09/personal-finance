using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Categories;

namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>
/// Gives a user the default categories (docs/database/default-data.md). Called right after a user is created: by the
/// startup code of the initial user and by registration ([Backend]).
/// </summary>
/// <remarks>
/// It works in its own context acting for that user, so the query filter, the interceptor and the composite keys
/// apply as for any other data. A user who already has categories gets nothing: running it twice is harmless.
/// </remarks>
public sealed class DefaultCategoriesInitializer(DbContextOptions<AppDbContext> options)
{
    /// <returns>Whether categories were created.</returns>
    public async Task<bool> EnsureForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = new AppDbContext(options, new FixedCurrentUser(userId));

        if (await db.Categories.AnyAsync(cancellationToken))
        {
            return false;
        }

        db.Categories.AddRange(DefaultCategories.CreateFor(userId));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
