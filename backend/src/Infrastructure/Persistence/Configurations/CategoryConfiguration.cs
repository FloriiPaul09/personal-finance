using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Categories;

namespace PersonalFinance.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", table =>
            table.HasCheckConstraint("ck_categories_kind", SnakeCaseEnumConverter<CategoryKind>.CheckSql("kind")));

        builder.MapOwnedEntity();

        builder.Property(category => category.Name).HasMaxLength(Category.NameMaxLength);
        builder.Property(category => category.NormalizedName).HasMaxLength(Category.NameMaxLength);
        builder.Property(category => category.Kind).HasConversion<SnakeCaseEnumConverter<CategoryKind>>();

        // Parent of the same user; archived, never deleted while it has subcategories.
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(category => new { category.UserId, category.ParentId })
            .HasPrincipalKey(parent => new { parent.UserId, parent.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        // Two top-level categories of the same kind with the same name collide too.
        builder.HasIndex(category => new { category.UserId, category.Kind, category.ParentId, category.NormalizedName })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}
