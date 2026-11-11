using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Common;

namespace PersonalFinance.Domain.Transactions;

/// <summary>
/// Money moved between two accounts of the same user: two legs in <c>transactions</c>, negative on the source and
/// positive on the destination, same date and description (docs/database/domain-model.md#transfer).
/// </summary>
public sealed class Transfer : OwnedEntity
{
    private readonly List<Transaction> _legs = [];

    public Transfer(Account from, Account to, DateOnly date, decimal amount, string? description = null)
        : base(from.UserId)
    {
        var positive = EnsureCanTransfer(from, to, amount);
        from.EnsureActive();
        to.EnsureActive();

        _legs.Add(Transaction.TransferLeg(from, Id, date, -positive, description));
        _legs.Add(Transaction.TransferLeg(to, Id, date, positive, description));
    }

    public IReadOnlyList<Transaction> Legs => _legs;

    public Transaction Outgoing => _legs.Single(leg => leg.Amount < 0);

    public Transaction Incoming => _legs.Single(leg => leg.Amount > 0);

    public DateOnly Date => Outgoing.Date;

    /// <summary>The amount moved, always positive.</summary>
    public decimal Amount => Incoming.Amount;

    public string? Description => Outgoing.Description;

    /// <summary>Changes both legs together. Archived accounts are refused only when newly chosen.</summary>
    public void Change(Account from, Account to, DateOnly date, decimal amount, string? description)
    {
        var outgoing = Outgoing;
        var incoming = Incoming;

        var positive = EnsureCanTransfer(from, to, amount);
        if (from.Id != outgoing.AccountId)
        {
            from.EnsureActive();
        }

        if (to.Id != incoming.AccountId)
        {
            to.EnsureActive();
        }

        outgoing.ChangeLeg(from, date, -positive, description);
        incoming.ChangeLeg(to, date, positive, description);
    }

    private decimal EnsureCanTransfer(Account from, Account to, decimal amount)
    {
        EnsureSameUser(from);
        EnsureSameUser(to);

        if (from.Id == to.Id)
        {
            throw new DomainException(DomainErrors.TransferSameAccount, "A transfer needs two different accounts.");
        }

        if (from.Currency != to.Currency)
        {
            throw new DomainException(DomainErrors.TransferCurrencyMismatch, "Transfers between currencies are not supported yet.");
        }

        return Amounts.EnsurePositive(amount);
    }
}
