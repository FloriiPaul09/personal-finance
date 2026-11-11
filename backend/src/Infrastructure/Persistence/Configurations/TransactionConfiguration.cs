using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Transactions;

namespace PersonalFinance.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions", table =>
        {
            table.HasCheckConstraint("ck_transactions_kind", SnakeCaseEnumConverter<TransactionKind>.CheckSql("kind"));
            table.HasCheckConstraint("ck_transactions_amount_not_zero", "amount <> 0");
            table.HasCheckConstraint(
                "ck_transactions_amount_sign",
                "(kind <> 'income' OR amount > 0) AND (kind <> 'expense' OR amount < 0)");
            table.HasCheckConstraint("ck_transactions_transfer", "(kind = 'transfer') = (transfer_id IS NOT NULL)");
            table.HasCheckConstraint("ck_transactions_category", "(kind = 'transfer') = (category_id IS NULL)");
        });

        builder.MapOwnedEntity();

        builder.Property(transaction => transaction.Kind).HasConversion<SnakeCaseEnumConverter<TransactionKind>>();
        builder.Property(transaction => transaction.Amount).HasPrecision(18, 2);
        builder.Property(transaction => transaction.Description).HasMaxLength(Transaction.DescriptionMaxLength);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(transaction => new { transaction.UserId, transaction.AccountId })
            .HasPrincipalKey(account => new { account.UserId, account.Id })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(transaction => new { transaction.UserId, transaction.CategoryId })
            .HasPrincipalKey(category => new { category.UserId, category.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(transaction => transaction.Tags)
            .WithOne()
            .HasForeignKey(link => new { link.UserId, link.TransactionId })
            .HasPrincipalKey(transaction => new { transaction.UserId, transaction.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(transaction => transaction.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Transaction list by period, newest first.
        builder.HasIndex(transaction => new { transaction.UserId, transaction.Date, transaction.Id })
            .IsDescending(false, true, false);

        // Balances and list by account; also covers the foreign key to accounts.
        builder.HasIndex(transaction => new { transaction.UserId, transaction.AccountId, transaction.Date });

        // Spending per category and budget progress; also covers the foreign key to categories.
        builder.HasIndex(transaction => new { transaction.UserId, transaction.CategoryId, transaction.Date });

        // At most one outgoing and one incoming leg per transfer.
        builder.HasIndex(transaction => transaction.TransferId, "ix_transactions_transfer_id_outgoing")
            .IsUnique()
            .HasFilter("transfer_id IS NOT NULL AND amount < 0");

        builder.HasIndex(transaction => transaction.TransferId, "ix_transactions_transfer_id_incoming")
            .IsUnique()
            .HasFilter("transfer_id IS NOT NULL AND amount > 0");
    }
}
