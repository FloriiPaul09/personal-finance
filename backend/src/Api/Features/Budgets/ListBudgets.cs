using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Responses;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Budgets;

/// <summary>
/// <c>GET /api/budgets</c>: the budget history, by category name and then by month; <c>?categoryId=</c> keeps one
/// category. Not paged: a category gets a few entries a year at most.
/// </summary>
public static class ListBudgets
{
    internal sealed class Endpoint : IEndpoint<BudgetsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("", Handle).WithName(nameof(ListBudgets));
    }

    internal static async Task<Ok<ListResponse<BudgetResponse>>> Handle(
        AppDbContext db, CancellationToken cancellationToken, Guid? categoryId = null)
    {
        var budgets = categoryId is { } category
            ? db.Budgets.Where(budget => budget.CategoryId == category)
            : db.Budgets;

        var items = await budgets
            .Join(db.Categories, budget => budget.CategoryId, item => item.Id, (budget, item) => new { Budget = budget, item.NormalizedName })
            .OrderBy(row => row.NormalizedName)
            .ThenBy(row => row.Budget.CategoryId)
            .ThenBy(row => row.Budget.EffectiveFrom)
            .Select(row => row.Budget)
            .SelectResponses()
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new ListResponse<BudgetResponse>(items));
    }
}
