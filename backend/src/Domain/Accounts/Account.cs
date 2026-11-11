using System.Diagnostics.CodeAnalysis;
using PersonalFinance.Domain.Common;
using PersonalFinance.Domain.Transactions;

namespace PersonalFinance.Domain.Accounts;

/// <summary>A container of money: cash, current account, card, savings (docs/database/domain-model.md).</summary>
public sealed class Account : OwnedEntity
{
    public const int NameMaxLength = 100;

    public Account(Guid userId, string name, AccountType type, string currency, decimal initialBalance)
        : base(userId)
    {
        Rename(name);
        ChangeType(type);
        Currency = Texts.EnsureCurrency(currency);
        InitialBalance = Amounts.EnsureValid(initialBalance);
    }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public AccountType Type { get; private set; }

    /// <summary>ISO 4217 code; every transaction of the account is in this currency.</summary>
    public string Currency { get; private set; }

    /// <summary>Any sign: a card or an overdraft may start below zero.</summary>
    public decimal InitialBalance { get; private set; }

    public bool IsArchived { get; private set; }

    [MemberNotNull(nameof(Name), nameof(NormalizedName))]
    public void Rename(string name)
    {
        Name = Texts.EnsureName(name, NameMaxLength);
        NormalizedName = Texts.NormalizeName(Name);
    }

    public void ChangeType(AccountType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainException(DomainErrors.AccountTypeInvalid, "Unknown account type.");
        }

        Type = type;
    }

    public void ChangeInitialBalance(decimal initialBalance) => InitialBalance = Amounts.EnsureValid(initialBalance);

    /// <summary>
    /// The currency can change only while the account has no movements: afterwards it would reinterpret every amount.
    /// </summary>
    /// <param name="hasMovements">Whether any transaction references the account (checked by the caller).</param>
    public void ChangeCurrency(string currency, bool hasMovements)
    {
        var code = Texts.EnsureCurrency(currency);
        if (code == Currency)
        {
            return;
        }

        if (hasMovements)
        {
            throw new DomainException(DomainErrors.AccountCurrencyLocked, "The currency of an account with movements cannot change.");
        }

        Currency = code;
    }

    /// <summary>An archived account keeps its history and balance but receives no new movements.</summary>
    public void Archive() => IsArchived = true;

    public void Unarchive() => IsArchived = false;

    public void EnsureActive()
    {
        if (IsArchived)
        {
            throw new DomainException(DomainErrors.AccountArchived, "An archived account cannot receive movements.");
        }
    }

    /// <summary>
    /// Initial balance plus the movements of the account dated up to <paramref name="upTo"/> (all of them when null).
    /// Never stored: the SQL query in Infrastructure computes the same sum.
    /// </summary>
    public decimal BalanceAt(IEnumerable<Transaction> transactions, DateOnly? upTo = null)
    {
        var balance = InitialBalance;

        foreach (var transaction in transactions)
        {
            if (transaction.AccountId != Id)
            {
                throw new DomainException(DomainErrors.AccountTransactionMismatch, "The transaction belongs to another account.");
            }

            if (upTo is null || transaction.Date <= upTo.Value)
            {
                balance += transaction.Amount;
            }
        }

        return balance;
    }
}
