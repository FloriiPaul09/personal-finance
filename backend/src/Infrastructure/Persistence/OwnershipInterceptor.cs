using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PersonalFinance.Domain.Common;
using PersonalFinance.Infrastructure.Identity;

namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>
/// Before every save: user data must belong to the current user and owners never change; sets the technical
/// timestamps from <see cref="TimeProvider"/> (docs/database/user-isolation.md#interceptor).
/// </summary>
public sealed class OwnershipInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Apply(DbContext? context)
    {
        if (context is not AppDbContext db)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached)
            {
                continue;
            }

            if (entry.Entity is ApplicationUser && entry.State == EntityState.Added)
            {
                entry.Property(nameof(ApplicationUser.CreatedAt)).CurrentValue = now;
                continue;
            }

            if (entry.Entity is not IOwnedByUser owned)
            {
                continue;
            }

            var currentUserId = db.CurrentUser.UserId
                ?? throw new UserIsolationException($"Changes to {entry.Metadata.DisplayName()} need a current user.");

            var originalOwner = entry.State == EntityState.Added
                ? owned.UserId
                : (Guid)entry.Property(nameof(IOwnedByUser.UserId)).OriginalValue!;

            if (originalOwner != currentUserId || owned.UserId != currentUserId)
            {
                throw new UserIsolationException($"{entry.Metadata.DisplayName()} belongs to another user or changes owner.");
            }

            if (entry.Entity is OwnedEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Property(nameof(OwnedEntity.CreatedAt)).CurrentValue = now;
                    entry.Property(nameof(OwnedEntity.UpdatedAt)).CurrentValue = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Property(nameof(OwnedEntity.UpdatedAt)).CurrentValue = now;
                }
            }
        }
    }
}
