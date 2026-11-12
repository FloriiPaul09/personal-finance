using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Requests;
using PersonalFinance.Api.Common.Responses;
using PersonalFinance.Domain.Budgets;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Budgets;

/// <summary>Progress of one budgeted category in a month (docs/backend/api-endpoints.md#budget-progress).</summary>
/// <param name="BudgetId">The entry that sets <see cref="Limit"/> this month.</param>
/// <param name="Spent">Expenses of the category and its subcategories in the month, positive.</param>
/// <param name="Remaining">Limit − spent; negative when over budget.</param>
/// <param name="Ratio">Spent / limit, exact; above 1 when over budget. Round only when displaying.</param>
public sealed record BudgetProgressResponse(
    Guid CategoryId,
    Guid BudgetId,
    YearMonth Month,
    decimal Limit,
    decimal Spent,
    decimal Remaining,
    decimal Ratio,
    bool IsOverBudget);

/// <summary>
/// <c>GET /api/budgets/progress?month=2026-11</c>: every category with a budget that month, by category name. Without
/// <c>month</c>, the current month of the server (UTC): the app sends its own month near midnight on the last day.
/// </summary>
public static class GetBudgetProgress
{
    internal sealed class Endpoint : IEndpoint<BudgetsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("progress", Handle).WithName(nameof(GetBudgetProgress));
    }

    internal static async Task<Ok<ListResponse<BudgetProgressResponse>>> Handle(
        AppDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken, string? month = null)
    {
        var selected = QueryValues.ParseMonth(month, nameof(month))
            ?? YearMonth.Of(DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));

        var items = await db.ProgressOf(selected).ToListAsync(cancellationToken);
        return TypedResults.Ok(new ListResponse<BudgetProgressResponse>(items.Select(row => row.ToResponse(selected)).ToList()));
    }
}

/// <summary>Limit and spending per budgeted category in one query; reused by the dashboard.</summary>
internal sealed record BudgetProgressRow(Guid CategoryId, Guid BudgetId, decimal Limit, decimal Spent)
{
    /// <summary>Remaining, ratio and over-budget come from the domain, never computed twice.</summary>
    public BudgetProgressResponse ToResponse(YearMonth month)
    {
        var progress = new BudgetProgress(month, Limit, Spent);
        return new BudgetProgressResponse(
            CategoryId, BudgetId, month, progress.Limit, progress.Spent, progress.Remaining, progress.Ratio, progress.IsOverBudget);
    }
}

internal static class BudgetProgressQueries
{
    /// <summary>
    /// The same rules as <c>BudgetProgress.Calculate</c>, in SQL: for each category, the entry with the latest month not
    /// after <paramref name="month"/> (no entry, or one without amount, means no budget); spent = −Σ expenses of the
    /// category and its subcategories dated in the month.
    /// </summary>
    public static IQueryable<BudgetProgressRow> ProgressOf(this AppDbContext db, YearMonth month)
    {
        var firstDay = month.FirstDay;
        var lastDay = month.LastDay;

        return db.Budgets
            .Where(entry => entry.EffectiveFrom <= firstDay
                && !db.Budgets.Any(later => later.CategoryId == entry.CategoryId
                    && later.EffectiveFrom > entry.EffectiveFrom
                    && later.EffectiveFrom <= firstDay))
            .Where(entry => entry.Amount != null)
            .Join(db.Categories, entry => entry.CategoryId, category => category.Id, (entry, category) => new { Entry = entry, category.NormalizedName })
            .OrderBy(row => row.NormalizedName)
            .ThenBy(row => row.Entry.CategoryId)
            .Select(row => new BudgetProgressRow(
                row.Entry.CategoryId,
                row.Entry.Id,
                row.Entry.Amount!.Value,
                -(db.Transactions
                    .Where(transaction => transaction.Kind == TransactionKind.Expense
                        && transaction.Date >= firstDay
                        && transaction.Date <= lastDay
                        && (transaction.CategoryId == row.Entry.CategoryId
                            || db.Categories.Any(child => child.Id == transaction.CategoryId && child.ParentId == row.Entry.CategoryId)))
                    .Sum(transaction => (decimal?)transaction.Amount) ?? 0m)));
    }
}
