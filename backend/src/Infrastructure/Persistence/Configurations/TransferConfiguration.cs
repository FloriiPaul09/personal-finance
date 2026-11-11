using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalFinance.Domain.Transactions;

namespace PersonalFinance.Infrastructure.Persistence.Configurations;

internal sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers");

        builder.MapOwnedEntity();

        // Date, amount and description live in the legs.
        builder.Ignore(transfer => transfer.Outgoing);
        builder.Ignore(transfer => transfer.Incoming);
        builder.Ignore(transfer => transfer.Date);
        builder.Ignore(transfer => transfer.Amount);
        builder.Ignore(transfer => transfer.Description);

        // A transfer is deleted with its legs.
        builder.HasMany(transfer => transfer.Legs)
            .WithOne()
            .HasForeignKey(leg => new { leg.UserId, leg.TransferId })
            .HasPrincipalKey(transfer => new { transfer.UserId, transfer.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(transfer => transfer.Legs).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
