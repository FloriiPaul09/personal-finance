using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Accounts;

namespace PersonalFinance.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", table =>
        {
            table.HasCheckConstraint("ck_accounts_type", SnakeCaseEnumConverter<AccountType>.CheckSql("type"));
            table.HasCheckConstraint("ck_accounts_currency", "currency ~ '^[A-Z]{3}$'");
        });

        builder.MapOwnedEntity();

        builder.Property(account => account.Name).HasMaxLength(Account.NameMaxLength);
        builder.Property(account => account.NormalizedName).HasMaxLength(Account.NameMaxLength);
        builder.Property(account => account.Type).HasConversion<SnakeCaseEnumConverter<AccountType>>();
        builder.Property(account => account.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(account => account.InitialBalance).HasPrecision(18, 2);

        builder.HasIndex(account => new { account.UserId, account.NormalizedName }).IsUnique();
    }
}
