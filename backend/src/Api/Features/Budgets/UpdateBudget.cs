using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Budgets;

/// <summary>
/// <c>PUT /api/budgets/{id}</c>, with <c>If-Match</c>: changes the amount of an entry; <c>null</c> (or omitted) turns it
/// into the end of the budget. Category and month never change: delete the entry and create another.
/// </summary>
public static class UpdateBudget
{
    /// <param name="Amount">Monthly limit, greater than zero; null ends the budget from the month of the entry.</param>
    public sealed record Request(decimal? Amount);

    internal sealed class Endpoint : IEndpoint<BudgetsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapPut("{id:guid}", Handle).WithName(nameof(UpdateBudget));
    }

    internal static async Task<Results<Ok<BudgetResponse>, NotFound>> Handle(
        Guid id, Request request, IfMatch ifMatch, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var budget = await db.Budgets.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (budget is null)
        {
            return TypedResults.NotFound();
        }

        db.EnsureVersion(budget, ifMatch);
        budget.ChangeAmount(request.Amount);
        await db.SaveChangesAsync(cancellationToken);

        var updated = await db.FindBudgetResponseAsync(budget.Id, cancellationToken)
            ?? throw new InvalidOperationException("The budget just updated cannot be read.");

        response.SetVersion(updated.Version);
        return TypedResults.Ok(updated);
    }
}
