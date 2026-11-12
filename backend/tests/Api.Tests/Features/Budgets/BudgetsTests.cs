using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Json;
using PersonalFinance.Api.Common.Responses;
using PersonalFinance.Api.Features.Budgets;
using PersonalFinance.Api.Tests.Support;
using PersonalFinance.Domain.Budgets;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Common;
using PersonalFinance.Testing;

namespace PersonalFinance.Api.Tests.Features.Budgets;

// Module budgets (docs/backend/api-endpoints.md#budgets): every endpoint with success, 401, validation, isolation
// and, for writes, versions (docs/backend/testing.md#minimum-per-endpoint); progress compared with the domain.
public sealed class BudgetsTests(ApiServer server) : ApiTest(server)
{
    private const string Budgets = "/api/budgets";

    private static readonly YearMonth January = new(2026, 1);
    private static readonly YearMonth June = new(2026, 6);
    private static readonly YearMonth September = new(2026, 9);

    // ---- List --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task List_ByCategoryNameThenMonth_WithMonthsAsText()
    {
        var userId = await NewUserAsync();
        var travel = await Database.CreateCategoryAsync(userId, name: "Viaggi", cancellationToken: Ct);
        var home = await Database.CreateCategoryAsync(userId, name: "casa", cancellationToken: Ct);
        var travelJune = await Database.CreateBudgetAsync(travel, 500m, June, Ct);
        var homeJune = await Database.CreateBudgetAsync(home, 350m, June, Ct);
        var homeJanuary = await Database.CreateBudgetAsync(home, 300m, January, Ct);
        var homeEnd = await Database.CreateBudgetAsync(home, null, September, Ct);

        var response = await ClientFor(userId).GetAsync(Budgets, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<JsonElement>();
        Assert.Equal("2026-01", body.GetProperty("items")[0].GetProperty("from").GetString());
        var items = body.Deserialize<ListResponse<BudgetResponse>>(JsonConventions.Options)!.Items;
        Assert.Equal(new[] { homeJanuary.Id, homeJune.Id, homeEnd.Id, travelJune.Id }, items.Select(item => item.Id));
        Assert.Null(items[2].Amount);
    }

    [Fact]
    public async Task List_FiltersByCategory_AndShowsOnlyTheUsersBudgets()
    {
        var userId = await NewUserAsync();
        var home = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var travel = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var homeBudget = await Database.CreateBudgetAsync(home, cancellationToken: Ct);
        await Database.CreateBudgetAsync(travel, cancellationToken: Ct);
        var otherCategory = await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct);
        await Database.CreateBudgetAsync(otherCategory, cancellationToken: Ct);
        var client = ClientFor(userId);

        var filtered = await client.GetJsonAsync<ListResponse<BudgetResponse>>($"{Budgets}?categoryId={home.Id}");
        var other = await client.GetJsonAsync<ListResponse<BudgetResponse>>($"{Budgets}?categoryId={otherCategory.Id}");
        var all = await client.GetJsonAsync<ListResponse<BudgetResponse>>(Budgets);

        Assert.Equal(new[] { homeBudget.Id }, filtered.Items.Select(item => item.Id));
        Assert.Empty(other.Items);
        Assert.Equal(2, all.Items.Count);
    }

    [Fact]
    public async Task List_WithoutSession_Returns401()
    {
        var response = await AnonymousClient().GetAsync(Budgets, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Get ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Get_ReturnsTheEntryWithItsVersionAsETag()
    {
        var userId = await NewUserAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var budget = await Database.CreateBudgetAsync(category, 300m, June, Ct);

        var response = await ClientFor(userId).GetAsync($"{Budgets}/{budget.Id}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<BudgetResponse>();
        Assert.Equal(new BudgetResponse(budget.Id, category.Id, June, 300m, body.Version), body);
        Assert.Equal(EntityTag.Format(body.Version), response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task Get_WithoutSession_Returns401()
    {
        var budget = await Database.CreateBudgetAsync(await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct), cancellationToken: Ct);

        var response = await AnonymousClient().GetAsync($"{Budgets}/{budget.Id}", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Fact]
    public async Task Get_AnotherUsersBudget_Returns404()
    {
        var other = await Database.CreateBudgetAsync(await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct), cancellationToken: Ct);

        var response = await ClientFor(await NewUserAsync()).GetAsync($"{Budgets}/{other.Id}", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
    }

    // ---- Create ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_Returns201WithLocationAndETag()
    {
        var userId = await NewUserAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var client = ClientFor(userId);

        var response = await client.PostAsJsonAsync(Budgets, new { categoryId = category.Id, from = "2026-06", amount = 300m }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<BudgetResponse>();
        Assert.Equal(new BudgetResponse(created.Id, category.Id, June, 300m, created.Version), created);
        Assert.Equal(EntityTag.Format(created.Version), response.Headers.ETag?.ToString());
        Assert.Equal($"{Budgets}/{created.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(created, await ReadAsync(client, created.Id));
    }

    [Fact]
    public async Task Create_WithoutAmount_EndsTheBudget()
    {
        var userId = await NewUserAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateBudgetAsync(category, 300m, January, Ct);

        var response = await ClientFor(userId).PostAsJsonAsync(Budgets, new { categoryId = category.Id, from = "2026-09" }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null((await response.ReadAsync<BudgetResponse>()).Amount);
    }

    [Theory]
    [InlineData("categoryId")]
    [InlineData("from")]
    public async Task Create_WithoutARequiredField_Returns400OnThatField(string field)
    {
        var userId = await NewUserAsync();
        var body = Body(await Database.CreateCategoryAsync(userId, cancellationToken: Ct));
        body.Remove(field);

        var response = await ClientFor(userId).PostAsJsonAsync(Budgets, body, Ct);

        Problems.AssertField(await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed), field);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-06-01")]
    [InlineData("giugno")]
    public async Task Create_WithAMalformedMonth_Returns400RequestInvalid(string from)
    {
        var userId = await NewUserAsync();
        var body = Body(await Database.CreateCategoryAsync(userId, cancellationToken: Ct));
        body["from"] = from;

        var response = await ClientFor(userId).PostAsJsonAsync(Budgets, body, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.RequestInvalid);
    }

    [Theory]
    [InlineData(0, DomainErrors.AmountNotPositive)]
    [InlineData(10.001, DomainErrors.AmountTooManyDecimals)]
    public async Task Create_WithAnInvalidAmount_Returns400WithTheDomainCode(double amount, string code)
    {
        var userId = await NewUserAsync();
        var body = Body(await Database.CreateCategoryAsync(userId, cancellationToken: Ct));
        body["amount"] = (decimal)amount;

        var response = await ClientFor(userId).PostAsJsonAsync(Budgets, body, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Create_OnAnIncomeCategory_Returns400()
    {
        var userId = await NewUserAsync();
        var salary = await Database.CreateCategoryAsync(userId, kind: CategoryKind.Income, cancellationToken: Ct);

        var response = await ClientFor(userId).PostAsJsonAsync(Budgets, Body(salary), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, DomainErrors.BudgetCategoryNotExpense);
    }

    [Fact]
    public async Task Create_OnAnArchivedCategory_Returns409_ButEndingItsBudgetWorks()
    {
        var userId = await NewUserAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateBudgetAsync(category, 300m, January, Ct);
        var client = ClientFor(userId);
        await client.PostEmptyAsync($"/api/categories/{category.Id}/archive");

        var newLimit = await client.PostAsJsonAsync(Budgets, new { categoryId = category.Id, from = "2026-06", amount = 100m }, Ct);
        var end = await client.PostAsJsonAsync(Budgets, new { categoryId = category.Id, from = "2026-06" }, Ct);

        await Problems.AssertAsync(newLimit, HttpStatusCode.Conflict, DomainErrors.CategoryArchived);
        Assert.Equal(HttpStatusCode.Created, end.StatusCode);
    }

    [Fact]
    public async Task Create_ForAMonthThatAlreadyHasAnEntry_Returns409()
    {
        var userId = await NewUserAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateBudgetAsync(category, 300m, June, Ct);

        var response = await ClientFor(userId).PostAsJsonAsync(Budgets, new { categoryId = category.Id, from = "2026-06", amount = 400m }, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, ApiErrors.BudgetMonthTaken);
    }

    [Fact]
    public async Task Create_OnASubcategoryOfABudgetedParent_AndTheOpposite_Returns409()
    {
        var userId = await NewUserAsync();
        var home = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var furniture = await Database.CreateCategoryAsync(userId, parent: home, cancellationToken: Ct);
        var transport = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var fuel = await Database.CreateCategoryAsync(userId, parent: transport, cancellationToken: Ct);
        await Database.CreateBudgetAsync(home, cancellationToken: Ct);
        await Database.CreateBudgetAsync(fuel, cancellationToken: Ct);
        var client = ClientFor(userId);

        var underBudgetedParent = await client.PostAsJsonAsync(Budgets, Body(furniture), Ct);
        var overBudgetedChild = await client.PostAsJsonAsync(Budgets, Body(transport), Ct);

        await Problems.AssertAsync(underBudgetedParent, HttpStatusCode.Conflict, DomainErrors.BudgetBranchConflict);
        await Problems.AssertAsync(overBudgetedChild, HttpStatusCode.Conflict, DomainErrors.BudgetBranchConflict);
    }

    [Fact]
    public async Task Create_OnAnotherUsersCategory_Returns400OnCategoryId()
    {
        var other = await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct);

        var response = await ClientFor(await NewUserAsync()).PostAsJsonAsync(Budgets, Body(other), Ct);

        Problems.AssertField(await Problems.AssertAsync(response, HttpStatusCode.BadRequest, "category.not_found"), "categoryId");
    }

    [Fact]
    public async Task Create_WithoutSession_Returns401()
    {
        var category = await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct);

        var response = await AnonymousClient().PostAsJsonAsync(Budgets, Body(category), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Update ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Update_ChangesTheAmount_AndReturnsTheNewVersion()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var budget = await ReadAsync(client, (await Database.CreateBudgetAsync(category, 300m, June, Ct)).Id);

        var response = await client.PutWithVersionAsync($"{Budgets}/{budget.Id}", new { amount = 320.50m }, budget.Version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<BudgetResponse>();
        Assert.Equal(new BudgetResponse(budget.Id, category.Id, June, 320.50m, updated.Version), updated);
        Assert.NotEqual(budget.Version, updated.Version);
        Assert.Equal(EntityTag.Format(updated.Version), response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task Update_WithoutAmount_TurnsTheEntryIntoTheEndOfTheBudget()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var budget = await ReadAsync(client, (await Database.CreateBudgetAsync(await Database.CreateCategoryAsync(userId, cancellationToken: Ct), cancellationToken: Ct)).Id);

        var response = await client.PutWithVersionAsync($"{Budgets}/{budget.Id}", new { amount = (decimal?)null }, budget.Version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.ReadAsync<BudgetResponse>()).Amount);
    }

    [Fact]
    public async Task Update_WithAnInvalidAmount_Returns400()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var budget = await ReadAsync(client, (await Database.CreateBudgetAsync(await Database.CreateCategoryAsync(userId, cancellationToken: Ct), cancellationToken: Ct)).Id);

        var response = await client.PutWithVersionAsync($"{Budgets}/{budget.Id}", new { amount = -5m }, budget.Version);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, DomainErrors.AmountNotPositive);
    }

    [Fact]
    public async Task Update_WithoutIfMatch_Returns428()
    {
        var userId = await NewUserAsync();
        var budget = await Database.CreateBudgetAsync(await Database.CreateCategoryAsync(userId, cancellationToken: Ct), cancellationToken: Ct);

        var response = await ClientFor(userId).PutAsJsonAsync($"{Budgets}/{budget.Id}", new { amount = 1m }, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionRequired, ApiErrors.VersionRequired);
    }

    [Fact]
    public async Task Update_WithAStaleVersion_Returns412_AndChangesNothing()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var budget = await ReadAsync(client, (await Database.CreateBudgetAsync(await Database.CreateCategoryAsync(userId, cancellationToken: Ct), 300m, cancellationToken: Ct)).Id);

        var response = await client.PutWithVersionAsync($"{Budgets}/{budget.Id}", new { amount = 1m }, budget.Version + 1);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Equal(300m, (await ReadAsync(client, budget.Id)).Amount);
    }

    [Fact]
    public async Task Update_AnotherUsersBudget_Returns404()
    {
        var otherUserId = await NewUserAsync();
        var other = await ReadAsync(ClientFor(otherUserId), (await Database.CreateBudgetAsync(await Database.CreateCategoryAsync(otherUserId, cancellationToken: Ct), cancellationToken: Ct)).Id);

        var response = await ClientFor(await NewUserAsync()).PutWithVersionAsync($"{Budgets}/{other.Id}", new { amount = 1m }, other.Version);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
    }

    [Fact]
    public async Task Update_WithoutSession_Returns401()
    {
        var budget = await Database.CreateBudgetAsync(await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct), cancellationToken: Ct);

        var response = await AnonymousClient().PutWithVersionAsync($"{Budgets}/{budget.Id}", new { amount = 1m }, 1);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Delete ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_TheLastEntry_LetsTheBudgetMoveToTheSubcategories()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var home = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var furniture = await Database.CreateCategoryAsync(userId, parent: home, cancellationToken: Ct);
        var budget = await ReadAsync(client, (await Database.CreateBudgetAsync(home, cancellationToken: Ct)).Id);

        var response = await client.DeleteWithVersionAsync($"{Budgets}/{budget.Id}", budget.Version);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Budgets}/{budget.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Budgets, Body(furniture), Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutIfMatch_Returns428()
    {
        var userId = await NewUserAsync();
        var budget = await Database.CreateBudgetAsync(await Database.CreateCategoryAsync(userId, cancellationToken: Ct), cancellationToken: Ct);

        var response = await ClientFor(userId).DeleteAsync($"{Budgets}/{budget.Id}", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionRequired, ApiErrors.VersionRequired);
    }

    [Fact]
    public async Task Delete_WithAStaleVersion_Returns412_AndKeepsIt()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var budget = await ReadAsync(client, (await Database.CreateBudgetAsync(await Database.CreateCategoryAsync(userId, cancellationToken: Ct), cancellationToken: Ct)).Id);

        var response = await client.DeleteWithVersionAsync($"{Budgets}/{budget.Id}", budget.Version + 1);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Budgets}/{budget.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_AnotherUsersBudget_Returns404_AndKeepsIt()
    {
        var otherUserId = await NewUserAsync();
        var other = await ReadAsync(ClientFor(otherUserId), (await Database.CreateBudgetAsync(await Database.CreateCategoryAsync(otherUserId, cancellationToken: Ct), cancellationToken: Ct)).Id);

        var response = await ClientFor(await NewUserAsync()).DeleteWithVersionAsync($"{Budgets}/{other.Id}", other.Version);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
        Assert.Equal(HttpStatusCode.OK, (await ClientFor(otherUserId).GetAsync($"{Budgets}/{other.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutSession_Returns401()
    {
        var budget = await Database.CreateBudgetAsync(await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct), cancellationToken: Ct);

        var response = await AnonymousClient().DeleteWithVersionAsync($"{Budgets}/{budget.Id}", 1);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Progress ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Progress_UsesTheLatestEntry_CountsSubcategories_OnlyInTheMonth_AndEqualsTheDomain()
    {
        var userId = await NewUserAsync();
        var account = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var transport = await Database.CreateCategoryAsync(userId, name: "Trasporti", cancellationToken: Ct);
        var fuel = await Database.CreateCategoryAsync(userId, name: "Carburante", parent: transport, cancellationToken: Ct);
        var unbudgeted = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var savings = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var january = await Database.CreateBudgetAsync(transport, 300m, January, Ct);
        var june = await Database.CreateBudgetAsync(transport, 350m, June, Ct);
        await Database.CreateExpenseAsync(account, 50m, new DateOnly(2026, 6, 1), transport, cancellationToken: Ct);
        await Database.CreateExpenseAsync(account, 70.25m, new DateOnly(2026, 6, 30), fuel, cancellationToken: Ct);
        await Database.CreateExpenseAsync(account, 999m, new DateOnly(2026, 7, 1), fuel, cancellationToken: Ct);
        await Database.CreateExpenseAsync(account, 20m, new DateOnly(2026, 5, 31), transport, cancellationToken: Ct);
        await Database.CreateExpenseAsync(account, 40m, new DateOnly(2026, 6, 15), unbudgeted, cancellationToken: Ct);
        await Database.CreateTransferAsync(account, savings, 500m, new DateOnly(2026, 6, 15), Ct);

        var response = await ClientFor(userId).GetAsync($"{Budgets}/progress?month=2026-06", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single((await response.ReadAsync<ListResponse<BudgetProgressResponse>>()).Items);
        Assert.Equal(new BudgetProgressResponse(transport.Id, june.Id, June, 350m, 120.25m, 229.75m, 120.25m / 350m, false), item);

        await using var db = Database.CreateContext(userId);
        var expected = BudgetProgress.Calculate(
            transport,
            [january, june],
            await db.Categories.ToListAsync(Ct),
            await db.Transactions.ToListAsync(Ct),
            June)!;
        Assert.Equal((expected.Limit, expected.Spent, expected.Remaining, expected.Ratio), (item.Limit, item.Spent, item.Remaining, item.Ratio));
    }

    [Fact]
    public async Task Progress_SkipsMonthsBeforeTheFirstEntry_AndEndedBudgets()
    {
        var userId = await NewUserAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateBudgetAsync(category, 300m, January, Ct);
        await Database.CreateBudgetAsync(category, null, September, Ct);
        var client = ClientFor(userId);

        var before = await client.GetJsonAsync<ListResponse<BudgetProgressResponse>>($"{Budgets}/progress?month=2025-12");
        var during = await client.GetJsonAsync<ListResponse<BudgetProgressResponse>>($"{Budgets}/progress?month=2026-08");
        var after = await client.GetJsonAsync<ListResponse<BudgetProgressResponse>>($"{Budgets}/progress?month=2026-10");

        Assert.Empty(before.Items);
        Assert.Equal(300m, Assert.Single(during.Items).Limit);
        Assert.Empty(after.Items);
    }

    [Fact]
    public async Task Progress_OverBudget_HasNegativeRemaining()
    {
        var userId = await NewUserAsync();
        var account = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateBudgetAsync(category, 100m, June, Ct);
        await Database.CreateExpenseAsync(account, 150m, new DateOnly(2026, 6, 10), category, cancellationToken: Ct);

        var item = Assert.Single((await ClientFor(userId).GetJsonAsync<ListResponse<BudgetProgressResponse>>($"{Budgets}/progress?month=2026-06")).Items);

        Assert.True(item.IsOverBudget);
        Assert.Equal(-50m, item.Remaining);
        Assert.Equal(1.5m, item.Ratio);
    }

    [Fact]
    public async Task Progress_WithoutMonth_UsesTheCurrentMonth()
    {
        var userId = await NewUserAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var current = YearMonth.Of(DateOnly.FromDateTime(DateTime.UtcNow));
        await Database.CreateBudgetAsync(category, 100m, current, Ct);

        var item = Assert.Single((await ClientFor(userId).GetJsonAsync<ListResponse<BudgetProgressResponse>>($"{Budgets}/progress")).Items);

        Assert.Equal(current, item.Month);
    }

    [Fact]
    public async Task Progress_WithAMalformedMonth_Returns400OnMonth()
    {
        var userId = await NewUserAsync();

        var response = await ClientFor(userId).GetAsync($"{Budgets}/progress?month=2026-6", Ct);

        Problems.AssertField(await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.RequestInvalid), "month");
    }

    [Fact]
    public async Task Progress_ContainsOnlyTheBudgetsOfTheUser()
    {
        var userId = await NewUserAsync();
        var mine = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateBudgetAsync(mine, 100m, June, Ct);
        await Database.CreateBudgetAsync(await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct), 100m, June, Ct);

        var items = (await ClientFor(userId).GetJsonAsync<ListResponse<BudgetProgressResponse>>($"{Budgets}/progress?month=2026-06")).Items;

        Assert.Equal(new[] { mine.Id }, items.Select(item => item.CategoryId));
    }

    [Fact]
    public async Task Progress_WithoutSession_Returns401()
    {
        var response = await AnonymousClient().GetAsync($"{Budgets}/progress", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Helpers -----------------------------------------------------------------------------------------------

    /// <summary>A valid create body; tests remove or replace one field.</summary>
    private static Dictionary<string, object?> Body(Category category) => new()
    {
        ["categoryId"] = category.Id,
        ["from"] = "2026-06",
        ["amount"] = 300m,
    };

    private static Task<BudgetResponse> ReadAsync(HttpClient client, Guid id) =>
        client.GetJsonAsync<BudgetResponse>($"{Budgets}/{id}");
}
