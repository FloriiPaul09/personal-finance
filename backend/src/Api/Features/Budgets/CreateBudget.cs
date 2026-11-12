using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Domain.Budgets;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Budgets;

/// <summary>
/// <c>POST /api/budgets</c>: a new entry of a category's budget, from a month on. One entry per category and month
/// (<c>409</c> <c>budget.month_taken</c>: change that entry instead); a budget sits on a parent category or on its
/// subcategories, never on both (<c>409</c> <c>budget.branch_conflict</c>).
/// </summary>
public static class CreateBudget
{
    /// <param name="CategoryId">An expense category of the user.</param>
    /// <param name="From">First month, <c>"yyyy-MM"</c>.</param>
    /// <param name="Amount">Monthly limit, greater than zero; null ends the budget from <paramref name="From"/>.</param>
    public sealed record Request(
        [Required] Guid? CategoryId,
        [Required] YearMonth? From,
        decimal? Amount);

    internal sealed class Endpoint : IEndpoint<BudgetsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPost("", Handle).WithName(nameof(CreateBudget));
    }

    internal static async Task<Created<BudgetResponse>> Handle(
        Request request, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var category = await db.Categories.GetReferenceAsync(request.CategoryId!.Value, "category", "categoryId", cancellationToken);

        // The rule needs the subcategories of the category and the categories that already have entries.
        var subcategories = await db.Categories.Where(item => item.ParentId == category.Id).ToListAsync(cancellationToken);
        var budgeted = await db.Budgets.Select(budget => budget.CategoryId).Distinct().ToListAsync(cancellationToken);
        BudgetPolicy.EnsureCanBudget(category, subcategories, budgeted.ToHashSet());

        var budget = new Budget(category, request.From!.Value, request.Amount);
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(cancellationToken);

        var created = await db.FindBudgetResponseAsync(budget.Id, cancellationToken)
            ?? throw new InvalidOperationException("The budget just created cannot be read.");

        response.SetVersion(created.Version);
        return TypedResults.Created($"{BudgetsModule.Path}/{budget.Id}", created);
    }
}
