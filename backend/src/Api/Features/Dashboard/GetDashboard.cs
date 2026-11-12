using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Requests;
using PersonalFinance.Api.Features.Accounts;
using PersonalFinance.Api.Features.Budgets;
using PersonalFinance.Domain.Budgets;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Api.Features.Dashboard;

/// <summary>Total balance of the accounts in one currency.</summary>
/// <param name="Balance">Every account in the currency, archived ones included (they keep their balance).</param>
/// <param name="ArchivedBalance">The part of <see cref="Balance"/> on archived accounts, which the account list does not show.</param>
public sealed record CurrencyBalanceResponse(string Currency, decimal Balance, decimal ArchivedBalance);

/// <summary>Everything the home page shows, for one month (docs/backend/api-endpoints.md#dashboard).</summary>
/// <param name="Accounts">Active accounts with their current balance, by name.</param>
/// <param name="Balances">Total balance per currency, archived accounts included.</param>
/// <param name="Totals">Income and expenses of the month per currency.</param>
/// <param name="Spending">Expenses of the month per top-level category and currency, largest first.</param>
/// <param name="Budgets">Progress of every budgeted category in the month.</param>
public sealed record DashboardResponse(
    YearMonth Month,
    IReadOnlyList<AccountResponse> Accounts,
    IReadOnlyList<CurrencyBalanceResponse> Balances,
    IReadOnlyList<PeriodTotalsResponse> Totals,
    IReadOnlyList<CategorySpendingResponse> Spending,
    IReadOnlyList<BudgetProgressResponse> Budgets);

/// <summary>
/// <c>GET /api/dashboard?month=2026-11</c>: the home page in one request (ADR-0020). Without <c>month</c>, the current
/// month of the server (UTC), as for budget progress.
/// </summary>
public static class GetDashboard
{
    internal sealed class Endpoint : IEndpoint<DashboardModule>
    {
        public void Map(IEndpointRouteBuilder module) => module.MapGet("", Handle).WithName(nameof(GetDashboard));
    }

    internal static async Task<Ok<DashboardResponse>> Handle(
        AppDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken, string? month = null)
    {
        var selected = QueryValues.ParseMonth(month, nameof(month))
            ?? YearMonth.Of(DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));

        // A user has a handful of accounts: one query, split in memory into the list and the totals.
        var accounts = await db.Accounts
            .OrderBy(account => account.NormalizedName)
            .ThenBy(account => account.Id)
            .SelectResponses(db)
            .ToListAsync(cancellationToken);

        var balances = accounts
            .GroupBy(account => account.Currency, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new CurrencyBalanceResponse(
                group.Key,
                group.Sum(account => account.Balance),
                group.Where(account => account.IsArchived).Sum(account => account.Balance)))
            .ToList();

        var totals = await db.TotalsOfAsync(selected.FirstDay, selected.LastDay, cancellationToken);
        var spending = await db.SpendingOfAsync(selected.FirstDay, selected.LastDay, cancellationToken);
        var budgets = (await db.ProgressOf(selected).ToListAsync(cancellationToken)).Select(row => row.ToResponse(selected)).ToList();

        return TypedResults.Ok(new DashboardResponse(
            selected,
            accounts.Where(account => !account.IsArchived).ToList(),
            balances,
            totals,
            spending,
            budgets));
    }
}
