using System.Diagnostics.CodeAnalysis;

namespace PersonalFinance.Api.Common.Errors;

/// <summary>
/// Unique indexes of the user data and the code returned when a save violates one (PostgreSQL <c>23505</c>):
/// a name already used, even by a request sent at the same time from another device (ADR-0013).
/// A test compares this list with the unique indexes of the database.
/// </summary>
internal static class UniqueConstraintErrors
{
    private static readonly Dictionary<string, string> Codes = new(StringComparer.Ordinal)
    {
        ["ix_accounts_user_id_normalized_name"] = ApiErrors.AccountNameTaken,
        ["ix_categories_user_id_kind_parent_id_normalized_name"] = ApiErrors.CategoryNameTaken,
        ["ix_tags_user_id_normalized_name"] = ApiErrors.TagNameTaken,
        ["ix_budgets_user_id_category_id_effective_from"] = ApiErrors.BudgetMonthTaken,
    };

    /// <summary>
    /// Unique indexes protecting invariants the domain already guarantees (one outgoing and one incoming leg per
    /// transfer): a violation is a bug and gives <c>500</c>.
    /// </summary>
    public static IReadOnlyCollection<string> Internal { get; } =
    [
        "ix_transactions_transfer_id_outgoing",
        "ix_transactions_transfer_id_incoming",
    ];

    public static IReadOnlyCollection<string> MappedIndexes => Codes.Keys;

    public static bool TryGetCode(string? constraintName, [NotNullWhen(true)] out string? code)
    {
        code = null;
        return constraintName is not null && Codes.TryGetValue(constraintName, out code);
    }
}
