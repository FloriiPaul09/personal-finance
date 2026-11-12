using Microsoft.EntityFrameworkCore;
using PersonalFinance.Domain.Budgets;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Budgets;

/// <summary>One entry of a category's budget history (docs/backend/api-endpoints.md#budgets).</summary>
/// <param name="From">First month the entry applies to; it lasts until the next entry of the category.</param>
/// <param name="Amount">Monthly limit; null ends the budget from <see cref="From"/>.</param>
/// <param name="Version">Row version, the value to send in <c>If-Match</c> (also in <c>ETag</c> on single reads).</param>
public sealed record BudgetResponse(Guid Id, Guid CategoryId, YearMonth From, decimal? Amount, uint Version);

internal static class BudgetQueries
{
    public static IQueryable<BudgetResponse> SelectResponses(this IQueryable<Budget> budgets) =>
        budgets.Select(budget => new BudgetResponse(
            budget.Id,
            budget.CategoryId,
            YearMonth.Of(budget.EffectiveFrom),
            budget.Amount,
            EF.Property<uint>(budget, RowVersion.PropertyName)));

    public static Task<BudgetResponse?> FindBudgetResponseAsync(this AppDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.Budgets.Where(budget => budget.Id == id).SelectResponses().SingleOrDefaultAsync(cancellationToken);
}
