using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Budgets;

/// <summary>
/// <c>DELETE /api/budgets/{id}</c>, with <c>If-Match</c>: removes a wrong entry from the history. To stop a budget and
/// keep its past, create an entry without amount instead. A category without entries is unbudgeted again, which is how
/// a budget moves between a parent and its subcategories.
/// </summary>
public static class DeleteBudget
{
    internal sealed class Endpoint : IEndpoint<BudgetsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapDelete("{id:guid}", Handle).WithName(nameof(DeleteBudget));
    }

    internal static async Task<Results<NoContent, NotFound>> Handle(
        Guid id, IfMatch ifMatch, AppDbContext db, CancellationToken cancellationToken)
    {
        var budget = await db.Budgets.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (budget is null)
        {
            return TypedResults.NotFound();
        }

        db.EnsureVersion(budget, ifMatch);
        db.Budgets.Remove(budget);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
