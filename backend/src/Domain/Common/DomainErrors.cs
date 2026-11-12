namespace PersonalFinance.Domain.Common;

/// <summary>Stable codes of <see cref="DomainException"/>, in the form <c>area.rule</c>.</summary>
public static class DomainErrors
{
    public const string UserRequired = "user.required";
    public const string DifferentUser = "user.different";

    public const string IdInvalid = "id.invalid";

    public const string AmountNotPositive = "amount.not_positive";
    public const string AmountTooManyDecimals = "amount.too_many_decimals";
    public const string AmountOutOfRange = "amount.out_of_range";

    public const string NameRequired = "name.required";
    public const string NameTooLong = "name.too_long";
    public const string DescriptionTooLong = "description.too_long";

    public const string CurrencyInvalid = "currency.invalid";
    public const string MonthInvalid = "month.invalid";

    public const string AccountTypeInvalid = "account.type_invalid";
    public const string AccountArchived = "account.archived";
    public const string AccountCurrencyLocked = "account.currency_locked";
    public const string AccountHasMovements = "account.has_movements";
    public const string AccountTransactionMismatch = "account.transaction_of_another_account";

    public const string CategoryKindInvalid = "category.kind_invalid";
    public const string CategoryArchived = "category.archived";
    public const string CategoryTooDeep = "category.too_deep";
    public const string CategoryParentItself = "category.parent_itself";
    public const string CategoryHasSubcategories = "category.has_subcategories";
    public const string CategoryKindMismatch = "category.kind_mismatch";
    public const string CategoryKindFollowsParent = "category.kind_follows_parent";
    public const string CategoryKindLocked = "category.kind_locked";
    public const string CategoryNotASubcategory = "category.not_a_subcategory";
    public const string CategoryInUse = "category.in_use";

    public const string TransactionIsTransferLeg = "transaction.is_transfer_leg";

    public const string TransferSameAccount = "transfer.same_account";
    public const string TransferCurrencyMismatch = "transfer.currency_mismatch";

    public const string BudgetCategoryNotExpense = "budget.category_not_expense";
    public const string BudgetBranchConflict = "budget.branch_conflict";
    public const string BudgetEntriesMismatch = "budget.entries_of_another_category";
}
