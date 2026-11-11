using PersonalFinance.Domain.Common;

namespace PersonalFinance.Domain.Transactions;

/// <summary>Link between a transaction and a tag of the same user (table <c>transaction_tags</c>).</summary>
public sealed class TransactionTag : IOwnedByUser
{
    internal TransactionTag(Transaction transaction, Tag tag)
    {
        UserId = transaction.UserId;
        TransactionId = transaction.Id;
        TagId = tag.Id;
    }

    /// <summary>Used by EF Core when materializing entities.</summary>
    private TransactionTag()
    {
    }

    public Guid UserId { get; private set; }

    public Guid TransactionId { get; private set; }

    public Guid TagId { get; private set; }
}
