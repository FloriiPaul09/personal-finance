using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Infrastructure.Identity;

namespace PersonalFinance.Infrastructure.Persistence.Configurations;

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");

        builder.MapOwnedEntity();

        builder.Property(tag => tag.Name).HasMaxLength(Tag.NameMaxLength);
        builder.Property(tag => tag.NormalizedName).HasMaxLength(Tag.NameMaxLength);

        builder.HasIndex(tag => new { tag.UserId, tag.NormalizedName }).IsUnique();
    }
}

internal sealed class TransactionTagConfiguration : IEntityTypeConfiguration<TransactionTag>
{
    public void Configure(EntityTypeBuilder<TransactionTag> builder)
    {
        builder.ToTable("transaction_tags");

        builder.HasKey(link => new { link.TransactionId, link.TagId });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(link => link.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a tag removes it from its transactions.
        builder.HasOne<Tag>()
            .WithMany()
            .HasForeignKey(link => new { link.UserId, link.TagId })
            .HasPrincipalKey(tag => new { tag.UserId, tag.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
