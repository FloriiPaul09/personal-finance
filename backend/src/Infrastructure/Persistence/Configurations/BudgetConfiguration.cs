using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Budgets;
using PersonalFinance.Domain.Categories;

namespace PersonalFinance.Infrastructure.Persistence.Configurations;

internal sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("budgets", table =>
        {
            table.HasCheckConstraint("ck_budgets_amount", "amount IS NULL OR amount > 0");
            table.HasCheckConstraint("ck_budgets_effective_from_first_day", "EXTRACT(DAY FROM effective_from) = 1");
        });

        builder.MapOwnedEntity();

        builder.Property(budget => budget.Amount).HasPrecision(18, 2);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(budget => new { budget.UserId, budget.CategoryId })
            .HasPrincipalKey(category => new { category.UserId, category.Id })
            .OnDelete(DeleteBehavior.NoAction);

        // One entry per category and month; also the lookup of a month's limit.
        builder.HasIndex(budget => new { budget.UserId, budget.CategoryId, budget.EffectiveFrom }).IsUnique();
    }
}
