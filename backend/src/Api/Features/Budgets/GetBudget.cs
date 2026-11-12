using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Budgets;

/// <summary><c>GET /api/budgets/{id}</c>: one budget entry; <c>ETag</c> with its version.</summary>
public static class GetBudget
{
    internal sealed class Endpoint : IEndpoint<BudgetsModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("{id:guid}", Handle).WithName(nameof(GetBudget));
    }

    internal static async Task<Results<Ok<BudgetResponse>, NotFound>> Handle(
        Guid id, AppDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var budget = await db.FindBudgetResponseAsync(id, cancellationToken);
        if (budget is null)
        {
            return TypedResults.NotFound();
        }

        response.SetVersion(budget.Version);
        return TypedResults.Ok(budget);
    }
}
