using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Dashboard;

/// <summary>Income and expenses of a period in one currency.</summary>
/// <param name="Expense">Positive.</param>
/// <param name="Net">Income − expense; negative when more was spent than earned.</param>
public sealed record PeriodTotalsResponse(string Currency, decimal Income, decimal Expense, decimal Net);

/// <summary>Expenses of a top-level category (subcategories included) in one currency.</summary>
/// <param name="Spent">Positive.</param>
public sealed record CategorySpendingResponse(Guid CategoryId, string Currency, decimal Spent);

/// <summary>
/// Aggregates of a period, each one query on the existing indexes. They take a date range, not a month, so charts over
/// several months can reuse them (ADR-0020). Transfers are never income or expenses; amounts are summed per currency of
/// their account, never across currencies.
/// </summary>
internal static class DashboardQueries
{
    public static async Task<IReadOnlyList<PeriodTotalsResponse>> TotalsOfAsync(
        this AppDbContext db, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await db.Transactions
            .Where(transaction => transaction.Date >= from && transaction.Date <= to && transaction.Kind != TransactionKind.Transfer)
            .Join(db.Accounts, transaction => transaction.AccountId, account => account.Id, (transaction, account) => new
            {
                account.Currency,
                transaction.Kind,
                transaction.Amount,
            })
            .GroupBy(row => row.Currency)
            .Select(group => new
            {
                Currency = group.Key,
                Income = group.Sum(row => row.Kind == TransactionKind.Income ? row.Amount : 0m),
                Expense = -group.Sum(row => row.Kind == TransactionKind.Expense ? row.Amount : 0m),
            })
            .OrderBy(row => row.Currency)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new PeriodTotalsResponse(row.Currency, row.Income, row.Expense, row.Income - row.Expense)).ToList();
    }

    /// <summary>Expenses per top-level category and currency, largest first: subcategories add up into their parent.</summary>
    public static async Task<IReadOnlyList<CategorySpendingResponse>> SpendingOfAsync(
        this AppDbContext db, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await db.Transactions
            .Where(transaction => transaction.Date >= from && transaction.Date <= to && transaction.Kind == TransactionKind.Expense)
            .Join(db.Accounts, transaction => transaction.AccountId, account => account.Id, (transaction, account) => new
            {
                transaction.CategoryId,
                account.Currency,
                transaction.Amount,
            })
            .Join(db.Categories, row => row.CategoryId, category => (Guid?)category.Id, (row, category) => new
            {
                TopCategoryId = category.ParentId ?? category.Id,
                row.Currency,
                row.Amount,
            })
            .GroupBy(row => new { row.TopCategoryId, row.Currency })
            .Select(group => new
            {
                group.Key.TopCategoryId,
                group.Key.Currency,
                Spent = -group.Sum(row => row.Amount),
            })
            .OrderByDescending(row => row.Spent)
            .ThenBy(row => row.Currency)
            .ThenBy(row => row.TopCategoryId)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new CategorySpendingResponse(row.TopCategoryId, row.Currency, row.Spent)).ToList();
    }
}
