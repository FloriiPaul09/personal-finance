namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>Names of the EF Core query filters. Named filters can be disabled one by one.</summary>
public static class QueryFilters
{
    /// <summary>
    /// Rows of the current user only, on every entity that implements <c>IOwnedByUser</c>.
    /// Slices in Api never disable it (docs/database/user-isolation.md#query-filter).
    /// </summary>
    public const string UserScope = "UserScope";
}
