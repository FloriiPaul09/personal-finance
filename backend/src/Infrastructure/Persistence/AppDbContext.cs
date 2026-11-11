using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Budgets;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Common;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Infrastructure.Identity;

namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>
/// The application database: Identity plus the domain tables. Every user-owned entity is filtered by the current user
/// (named filter <see cref="QueryFilters.UserScope"/>, docs/database/user-isolation.md).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    private static readonly PropertyInfo CurrentUserIdProperty =
        typeof(AppDbContext).GetProperty(nameof(CurrentUserId), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<Transfer> Transfers => Set<Transfer>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<TransactionTag> TransactionTags => Set<TransactionTag>();

    public DbSet<Budget> Budgets => Set<Budget>();

    internal ICurrentUser CurrentUser { get; } = currentUser;

    /// <summary>
    /// Read by the query filter at query time. <see cref="Guid.Empty"/> when nobody is acting: no row has an empty owner,
    /// so nothing matches.
    /// </summary>
    internal Guid CurrentUserId => CurrentUser.UserId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        RenameIdentityTables(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        AddUserScopeFilters(builder);
    }

    private static void RenameIdentityTables(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }

    // entity => entity.UserId == this.CurrentUserId, for every entity implementing IOwnedByUser.
    private void AddUserScopeFilters(ModelBuilder builder)
    {
        var ownedTypes = builder.Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(clrType => typeof(IOwnedByUser).IsAssignableFrom(clrType))
            .ToList();

        foreach (var clrType in ownedTypes)
        {
            var entity = Expression.Parameter(clrType, "entity");
            var filter = Expression.Lambda(
                Expression.Equal(
                    Expression.Property(entity, nameof(IOwnedByUser.UserId)),
                    Expression.Property(Expression.Constant(this), CurrentUserIdProperty)),
                entity);

            builder.Entity(clrType).HasQueryFilter(QueryFilters.UserScope, filter);
        }
    }
}
