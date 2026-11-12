using PersonalFinance.Domain.Common;

namespace PersonalFinance.Api.Common.Errors;

/// <summary>
/// HTTP status of every <see cref="DomainException"/> code (ADR-0013, docs/backend/validation-and-errors.md#domain-errors).
/// A test fails when a code of <see cref="DomainErrors"/> is missing here.
/// </summary>
internal static class DomainErrorStatuses
{
    /// <summary>The values sent cannot be accepted, whatever the state of the data.</summary>
    private static readonly string[] InvalidValues =
    [
        DomainErrors.AmountNotPositive,
        DomainErrors.AmountTooManyDecimals,
        DomainErrors.AmountOutOfRange,
        DomainErrors.NameRequired,
        DomainErrors.NameTooLong,
        DomainErrors.DescriptionTooLong,
        DomainErrors.CurrencyInvalid,
        DomainErrors.MonthInvalid,
        DomainErrors.AccountTypeInvalid,
        DomainErrors.CategoryKindInvalid,
        DomainErrors.CategoryTooDeep,
        DomainErrors.CategoryParentItself,
        DomainErrors.CategoryKindMismatch,
        DomainErrors.CategoryKindFollowsParent,
        DomainErrors.CategoryNotASubcategory,
        DomainErrors.TransferSameAccount,
        DomainErrors.TransferCurrencyMismatch,
        DomainErrors.BudgetCategoryNotExpense,
    ];

    /// <summary>The values are fine, but the current state of the data does not allow the change.</summary>
    private static readonly string[] StateConflicts =
    [
        DomainErrors.AccountArchived,
        DomainErrors.AccountCurrencyLocked,
        DomainErrors.CategoryArchived,
        DomainErrors.CategoryHasSubcategories,
        DomainErrors.CategoryKindLocked,
        DomainErrors.TransactionIsTransferLeg,
        DomainErrors.BudgetBranchConflict,
    ];

    /// <summary>
    /// Invariants that slices must guarantee before calling the domain: seeing one is a bug, answered with
    /// <c>500</c> and logged, never explained to the client.
    /// </summary>
    private static readonly string[] ProgrammingErrors =
    [
        DomainErrors.UserRequired,
        DomainErrors.DifferentUser,
        DomainErrors.AccountTransactionMismatch,
        DomainErrors.BudgetEntriesMismatch,
    ];

    private static readonly Dictionary<string, int> Statuses =
        InvalidValues.Select(code => (Code: code, Status: StatusCodes.Status400BadRequest))
            .Concat(StateConflicts.Select(code => (Code: code, Status: StatusCodes.Status409Conflict)))
            .Concat(ProgrammingErrors.Select(code => (Code: code, Status: StatusCodes.Status500InternalServerError)))
            .ToDictionary(entry => entry.Code, entry => entry.Status, StringComparer.Ordinal);

    public static IReadOnlyCollection<string> MappedCodes => Statuses.Keys;

    /// <summary><c>500</c> for codes that are not mapped: a missing entry is a bug, caught by the tests.</summary>
    public static int StatusOf(string code) =>
        Statuses.TryGetValue(code, out var status) ? status : StatusCodes.Status500InternalServerError;
}
