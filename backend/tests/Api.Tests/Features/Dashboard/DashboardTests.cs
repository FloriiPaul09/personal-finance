using System.Net;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Features.Budgets;
using PersonalFinance.Api.Features.Dashboard;
using PersonalFinance.Api.Tests.Support;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Budgets;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Testing;

namespace PersonalFinance.Api.Tests.Features.Dashboard;

// Module dashboard (docs/backend/api-endpoints.md#dashboard): one read-only endpoint with success, 401, validation of
// the month and isolation (docs/backend/testing.md#minimum-per-endpoint).
public sealed class DashboardTests(ApiServer server) : ApiTest(server)
{
    private const string Dashboard = "/api/dashboard";

    private static readonly YearMonth June = new(2026, 6);

    [Fact]
    public async Task Dashboard_ComputesEverySection_PerCurrency_WithoutTransfers()
    {
        var userId = await NewUserAsync();
        var checking = await Database.CreateAccountAsync(userId, name: "Conto corrente", initialBalance: 1000m, cancellationToken: Ct);
        var savings = await Database.CreateAccountAsync(userId, name: "Risparmi", type: AccountType.Savings, cancellationToken: Ct);
        var card = await Database.CreateAccountAsync(userId, name: "Carta vecchia", type: AccountType.CreditCard, initialBalance: -50m, cancellationToken: Ct);
        var dollars = await Database.CreateAccountAsync(userId, name: "Dollari", currency: "USD", initialBalance: 100m, cancellationToken: Ct);
        var home = await Database.CreateCategoryAsync(userId, name: "Casa", cancellationToken: Ct);
        var furniture = await Database.CreateCategoryAsync(userId, name: "Arredamento", parent: home, cancellationToken: Ct);
        var food = await Database.CreateCategoryAsync(userId, name: "Spesa", cancellationToken: Ct);
        var travel = await Database.CreateCategoryAsync(userId, name: "Viaggi", cancellationToken: Ct);
        var salary = await Database.CreateCategoryAsync(userId, name: "Stipendio", kind: CategoryKind.Income, cancellationToken: Ct);
        var client = ClientFor(userId);
        await client.PostEmptyAsync($"/api/accounts/{card.Id}/archive");

        await Database.CreateIncomeAsync(checking, 2000m, new DateOnly(2026, 6, 27), salary, cancellationToken: Ct);
        await Database.CreateExpenseAsync(checking, 100m, new DateOnly(2026, 6, 1), home, cancellationToken: Ct);
        await Database.CreateExpenseAsync(checking, 50.50m, new DateOnly(2026, 6, 30), furniture, cancellationToken: Ct);
        await Database.CreateExpenseAsync(checking, 30m, new DateOnly(2026, 6, 12), food, cancellationToken: Ct);
        await Database.CreateExpenseAsync(dollars, 20m, new DateOnly(2026, 6, 12), travel, cancellationToken: Ct);
        await Database.CreateExpenseAsync(checking, 999m, new DateOnly(2026, 5, 31), food, cancellationToken: Ct);
        await Database.CreateTransferAsync(checking, savings, 300m, new DateOnly(2026, 6, 15), Ct);
        var foodBudget = await Database.CreateBudgetAsync(food, 100m, new YearMonth(2026, 1), Ct);

        var response = await client.GetAsync($"{Dashboard}?month=2026-06", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<DashboardResponse>();
        Assert.Equal(June, body.Month);

        // Active accounts by name, with their current balance; the archived card only counts in the totals.
        Assert.Equal(
            new[] { (checking.Id, 1520.50m), (dollars.Id, 80m), (savings.Id, 300m) },
            body.Accounts.Select(account => (account.Id, account.Balance)));
        Assert.Equal(
            new[] { new CurrencyBalanceResponse("EUR", 1770.50m, -50m), new CurrencyBalanceResponse("USD", 80m, 0m) },
            body.Balances);

        // The transfer of June is neither income nor expense; May is outside the month.
        Assert.Equal(
            new[] { new PeriodTotalsResponse("EUR", 2000m, 180.50m, 1819.50m), new PeriodTotalsResponse("USD", 0m, 20m, -20m) },
            body.Totals);

        // Furniture adds up into Casa; dollars stay apart from euros.
        Assert.Equal(
            new[] { new CategorySpendingResponse(home.Id, "EUR", 150.50m), new CategorySpendingResponse(food.Id, "EUR", 30m), new CategorySpendingResponse(travel.Id, "USD", 20m) },
            body.Spending);

        Assert.Equal(
            new[] { new BudgetProgressResponse(food.Id, foodBudget.Id, June, 100m, 30m, 70m, 0.3m, false) },
            body.Budgets);
    }

    [Fact]
    public async Task Dashboard_OfAUserWithoutData_HasEmptySections()
    {
        var userId = await NewUserAsync();

        var body = await ClientFor(userId).GetJsonAsync<DashboardResponse>($"{Dashboard}?month=2026-06");

        Assert.Equal(June, body.Month);
        Assert.Empty(body.Accounts);
        Assert.Empty(body.Balances);
        Assert.Empty(body.Totals);
        Assert.Empty(body.Spending);
        Assert.Empty(body.Budgets);
    }

    [Fact]
    public async Task Dashboard_WithoutMonth_UsesTheCurrentMonth()
    {
        var userId = await NewUserAsync();

        var body = await ClientFor(userId).GetJsonAsync<DashboardResponse>(Dashboard);

        Assert.Equal(YearMonth.Of(DateOnly.FromDateTime(DateTime.UtcNow)), body.Month);
    }

    [Theory]
    [InlineData("2026-6")]
    [InlineData("giugno")]
    public async Task Dashboard_WithAMalformedMonth_Returns400OnMonth(string month)
    {
        var userId = await NewUserAsync();

        var response = await ClientFor(userId).GetAsync($"{Dashboard}?month={month}", Ct);

        Problems.AssertField(await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.RequestInvalid), "month");
    }

    [Fact]
    public async Task Dashboard_ShowsOnlyTheDataOfTheUser()
    {
        var otherUserId = await NewUserAsync();
        var otherAccount = await Database.CreateAccountAsync(otherUserId, initialBalance: 500m, cancellationToken: Ct);
        var otherCategory = await Database.CreateCategoryAsync(otherUserId, cancellationToken: Ct);
        await Database.CreateExpenseAsync(otherAccount, 10m, new DateOnly(2026, 6, 10), otherCategory, cancellationToken: Ct);
        await Database.CreateIncomeAsync(otherAccount, 10m, new DateOnly(2026, 6, 10), cancellationToken: Ct);
        await Database.CreateBudgetAsync(otherCategory, 50m, June, Ct);
        var userId = await NewUserAsync();
        var mine = await Database.CreateAccountAsync(userId, cancellationToken: Ct);

        var body = await ClientFor(userId).GetJsonAsync<DashboardResponse>($"{Dashboard}?month=2026-06");

        Assert.Equal(new[] { mine.Id }, body.Accounts.Select(account => account.Id));
        Assert.Equal(new[] { new CurrencyBalanceResponse("EUR", 0m, 0m) }, body.Balances);
        Assert.Empty(body.Totals);
        Assert.Empty(body.Spending);
        Assert.Empty(body.Budgets);
    }

    [Fact]
    public async Task Dashboard_WithoutSession_Returns401()
    {
        var response = await AnonymousClient().GetAsync(Dashboard, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }
}
