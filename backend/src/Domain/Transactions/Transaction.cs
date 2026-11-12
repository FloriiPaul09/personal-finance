using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Common;

namespace PersonalFinance.Domain.Transactions;

/// <summary>
/// One movement on one account. The amount is signed: income positive, expense negative, transfer legs negative on
/// the source and positive on the destination (docs/database/domain-model.md#amounts). Callers always pass positive
/// amounts; the sign is applied here.
/// </summary>
public sealed class Transaction : OwnedEntity
{
    public const int DescriptionMaxLength = 200;

    private readonly List<TransactionTag> _tags = [];

    private Transaction(
        Account account,
        TransactionKind kind,
        DateOnly date,
        decimal signedAmount,
        Guid? categoryId,
        Guid? transferId,
        string? description,
        Guid? id = null)
        : base(account.UserId, id)
    {
        AccountId = account.Id;
        Kind = kind;
        Date = date;
        Amount = signedAmount;
        CategoryId = categoryId;
        TransferId = transferId;
        Description = Texts.EnsureDescription(description, DescriptionMaxLength);
    }

    /// <summary>Used by EF Core when materializing entities.</summary>
    private Transaction()
    {
    }

    public Guid AccountId { get; private set; }

    /// <summary>Never changes: an income cannot become an expense or a transfer leg.</summary>
    public TransactionKind Kind { get; private set; }

    /// <summary>Calendar date, no time and no time zone.</summary>
    public DateOnly Date { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>Required for income and expense, null for transfer legs.</summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>Set only for transfer legs.</summary>
    public Guid? TransferId { get; private set; }

    public string? Description { get; private set; }

    public IReadOnlyCollection<TransactionTag> Tags => _tags;

    /// <param name="id">Chosen by the caller (offline client, import), or null to generate one.</param>
    public static Transaction Income(Account account, Category category, DateOnly date, decimal amount, string? description = null, Guid? id = null)
    {
        EnsureCanRecord(account, category, TransactionKind.Income);
        return new Transaction(account, TransactionKind.Income, date, Amounts.EnsurePositive(amount), category.Id, null, description, id);
    }

    /// <param name="id">Chosen by the caller (offline client, import), or null to generate one.</param>
    public static Transaction Expense(Account account, Category category, DateOnly date, decimal amount, string? description = null, Guid? id = null)
    {
        EnsureCanRecord(account, category, TransactionKind.Expense);
        return new Transaction(account, TransactionKind.Expense, date, -Amounts.EnsurePositive(amount), category.Id, null, description, id);
    }

    /// <summary>
    /// Changes an income or an expense. Archived accounts and categories are refused only when newly chosen, so an
    /// existing movement on an archived account can still be corrected.
    /// </summary>
    public void Change(Account account, Category category, DateOnly date, decimal amount, string? description)
    {
        if (Kind == TransactionKind.Transfer)
        {
            throw new DomainException(DomainErrors.TransactionIsTransferLeg, "A transfer leg changes through its transfer.");
        }

        EnsureSameUser(account);
        EnsureCanUse(account, category, Kind, accountChanged: account.Id != AccountId, categoryChanged: category.Id != CategoryId);

        var positive = Amounts.EnsurePositive(amount);
        var newDescription = Texts.EnsureDescription(description, DescriptionMaxLength);

        AccountId = account.Id;
        CategoryId = category.Id;
        Date = date;
        Amount = Kind == TransactionKind.Income ? positive : -positive;
        Description = newDescription;
    }

    public void AddTag(Tag tag)
    {
        EnsureSameUser(tag);

        if (_tags.Exists(link => link.TagId == tag.Id))
        {
            return;
        }

        _tags.Add(new TransactionTag(this, tag));
    }

    public void RemoveTag(Guid tagId) => _tags.RemoveAll(link => link.TagId == tagId);

    /// <summary>Leaves exactly <paramref name="tags"/> on the transaction: links missing from it are removed, new ones added.</summary>
    public void ReplaceTags(IReadOnlyCollection<Tag> tags)
    {
        foreach (var tag in tags)
        {
            EnsureSameUser(tag);
        }

        var wanted = tags.Select(tag => tag.Id).ToHashSet();
        _tags.RemoveAll(link => !wanted.Contains(link.TagId));

        foreach (var tag in tags)
        {
            AddTag(tag);
        }
    }

    /// <summary>A transfer leg is deleted with its transfer, never alone: the other account would keep half a transfer.</summary>
    public void EnsureCanDelete()
    {
        if (Kind == TransactionKind.Transfer)
        {
            throw new DomainException(DomainErrors.TransactionIsTransferLeg, "A transfer leg is deleted through its transfer.");
        }
    }

    internal static Transaction TransferLeg(Account account, Guid transferId, DateOnly date, decimal signedAmount, string? description) =>
        new(account, TransactionKind.Transfer, date, signedAmount, null, transferId, description);

    internal void ChangeLeg(Account account, DateOnly date, decimal signedAmount, string? description)
    {
        var newDescription = Texts.EnsureDescription(description, DescriptionMaxLength);

        AccountId = account.Id;
        Date = date;
        Amount = signedAmount;
        Description = newDescription;
    }

    private static void EnsureCanRecord(Account account, Category category, TransactionKind kind) =>
        EnsureCanUse(account, category, kind, accountChanged: true, categoryChanged: true);

    private static void EnsureCanUse(Account account, Category category, TransactionKind kind, bool accountChanged, bool categoryChanged)
    {
        if (category.UserId != account.UserId)
        {
            throw new DomainException(DomainErrors.DifferentUser, "Data of different users cannot be combined.");
        }

        var expected = kind == TransactionKind.Income ? CategoryKind.Income : CategoryKind.Expense;
        if (category.Kind != expected)
        {
            throw new DomainException(DomainErrors.CategoryKindMismatch, $"A {kind.ToString().ToLowerInvariant()} needs a {expected.ToString().ToLowerInvariant()} category.");
        }

        if (accountChanged)
        {
            account.EnsureActive();
        }

        if (categoryChanged && category.IsArchived)
        {
            throw new DomainException(DomainErrors.CategoryArchived, "An archived category cannot classify new movements.");
        }
    }
}
