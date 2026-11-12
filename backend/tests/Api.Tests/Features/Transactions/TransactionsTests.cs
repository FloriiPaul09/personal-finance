using System.Net;
using System.Net.Http.Json;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Responses;
using PersonalFinance.Api.Features.Accounts;
using PersonalFinance.Api.Features.Tags;
using PersonalFinance.Api.Features.Transactions;
using PersonalFinance.Api.Tests.Support;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Common;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Testing;

namespace PersonalFinance.Api.Tests.Features.Transactions;

// Module transactions (docs/backend/api-endpoints.md#transactions): every endpoint with success, 401, validation,
// isolation and, for writes, versions (docs/backend/testing.md#minimum-per-endpoint).
public sealed class TransactionsTests(ApiServer server) : ApiTest(server)
{
    private const string Transactions = "/api/transactions";

    private static readonly DateOnly Day1 = new(2026, 11, 1);
    private static readonly DateOnly Day2 = new(2026, 11, 2);
    private static readonly DateOnly Day3 = new(2026, 11, 3);

    // ---- List --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task List_NewestFirst_WithPositiveAndSignedAmounts_AndTransferLegs()
    {
        var (userId, account) = await UserWithAccountAsync();
        var savings = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var expense = await Database.CreateExpenseAsync(account, 12.50m, Day1, cancellationToken: Ct);
        var income = await Database.CreateIncomeAsync(account, 1500m, Day3, cancellationToken: Ct);
        var transfer = await Database.CreateTransferAsync(account, savings, 200m, Day2, cancellationToken: Ct);

        var response = await ClientFor(userId).GetAsync($"{Transactions}?accountId={account.Id}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.ReadAsync<PageResponse<TransactionResponse>>();
        Assert.Equal(new[] { income.Id, transfer.Outgoing.Id, expense.Id }, page.Items.Select(item => item.Id));
        Assert.Null(page.NextCursor);

        var (incomeItem, legItem, expenseItem) = (page.Items[0], page.Items[1], page.Items[2]);
        Assert.Equal((TransactionKind.Income, 1500m, 1500m), (incomeItem.Kind, incomeItem.Amount, incomeItem.SignedAmount));
        Assert.Equal((TransactionKind.Expense, 12.50m, -12.50m), (expenseItem.Kind, expenseItem.Amount, expenseItem.SignedAmount));
        Assert.Equal((TransactionKind.Transfer, 200m, -200m), (legItem.Kind, legItem.Amount, legItem.SignedAmount));
        Assert.Equal(transfer.Id, legItem.TransferId);
        Assert.Null(legItem.CategoryId);
    }

    [Fact]
    public async Task List_PagesWithACursor_WithoutGapsOrDuplicates()
    {
        var (userId, account) = await UserWithAccountAsync();
        var created = new List<Transaction>();
        foreach (var date in new[] { Day1, Day1, Day2, Day2, Day2, Day3, Day3 })
        {
            created.Add(await Database.CreateExpenseAsync(account, 1m, date, cancellationToken: Ct));
        }

        var client = ClientFor(userId);
        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var uri = $"{Transactions}?limit=3" + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}");
            var page = await client.GetJsonAsync<PageResponse<TransactionResponse>>(uri);
            Assert.True(page.Items.Count <= 3);
            seen.AddRange(page.Items.Select(item => item.Id));
            cursor = page.NextCursor;
            pages++;
        }
        while (cursor is not null);

        // PostgreSQL orders uuids by their bytes, which is the order of their hexadecimal text.
        var expected = created
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.Id.ToString("N"), StringComparer.Ordinal)
            .Select(item => item.Id);
        Assert.Equal(3, pages);
        Assert.Equal(expected, seen);
    }

    [Fact]
    public async Task List_FiltersByPeriod()
    {
        var (userId, account) = await UserWithAccountAsync();
        await Database.CreateExpenseAsync(account, date: Day1, cancellationToken: Ct);
        var inside = await Database.CreateExpenseAsync(account, date: Day2, cancellationToken: Ct);
        await Database.CreateExpenseAsync(account, date: Day3, cancellationToken: Ct);

        var page = await ClientFor(userId).GetJsonAsync<PageResponse<TransactionResponse>>($"{Transactions}?from=2026-11-02&to=2026-11-02");

        Assert.Equal(new[] { inside.Id }, page.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task List_FiltersByAParentCategory_IncludingItsSubcategories()
    {
        var (userId, account) = await UserWithAccountAsync();
        var transport = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var fuel = await Database.CreateCategoryAsync(userId, parent: transport, cancellationToken: Ct);
        var onParent = await Database.CreateExpenseAsync(account, date: Day1, category: transport, cancellationToken: Ct);
        var onChild = await Database.CreateExpenseAsync(account, date: Day2, category: fuel, cancellationToken: Ct);
        await Database.CreateExpenseAsync(account, date: Day3, cancellationToken: Ct);
        var client = ClientFor(userId);

        var parentPage = await client.GetJsonAsync<PageResponse<TransactionResponse>>($"{Transactions}?categoryId={transport.Id}");
        var childPage = await client.GetJsonAsync<PageResponse<TransactionResponse>>($"{Transactions}?categoryId={fuel.Id}");

        Assert.Equal(new[] { onChild.Id, onParent.Id }, parentPage.Items.Select(item => item.Id));
        Assert.Equal(new[] { onChild.Id }, childPage.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task List_FiltersByKindTagAndText()
    {
        var (userId, account) = await UserWithAccountAsync();
        var holiday = await Database.CreateTagAsync(userId, "Vacanza-2026", Ct);
        var income = await Database.CreateIncomeAsync(account, date: Day1, cancellationToken: Ct);
        var tagged = await Database.CreateExpenseAsync(account, date: Day2, tags: [holiday], cancellationToken: Ct);
        var described = await Database.CreateExpenseAsync(account, date: Day3, description: "Sconto 50% al mercato", cancellationToken: Ct);
        await Database.CreateExpenseAsync(account, date: Day3, description: "Sconto 50 euro", cancellationToken: Ct);
        var client = ClientFor(userId);

        var incomes = await client.GetJsonAsync<PageResponse<TransactionResponse>>($"{Transactions}?kind=income");
        var withTag = await client.GetJsonAsync<PageResponse<TransactionResponse>>($"{Transactions}?tag=VACANZA-2026");
        var withText = await client.GetJsonAsync<PageResponse<TransactionResponse>>($"{Transactions}?q={Uri.EscapeDataString("50%")}");

        Assert.Equal(new[] { income.Id }, incomes.Items.Select(item => item.Id));
        Assert.Equal(new[] { tagged.Id }, withTag.Items.Select(item => item.Id));
        Assert.Equal(new[] { "Vacanza-2026" }, withTag.Items[0].Tags);

        // "%" is matched literally: "Sconto 50 euro" does not contain "50%".
        Assert.Equal(new[] { described.Id }, withText.Items.Select(item => item.Id));
    }

    [Theory]
    [InlineData("limit=0", "limit", ApiErrors.ValidationFailed)]
    [InlineData("limit=201", "limit", ApiErrors.ValidationFailed)]
    [InlineData("from=2026-11-02&to=2026-11-01", "to", ApiErrors.ValidationFailed)]
    [InlineData("kind=refund", "kind", ApiErrors.RequestInvalid)]
    [InlineData("cursor=not-a-cursor", "cursor", ApiErrors.RequestInvalid)]
    [InlineData("cursor=bm90X2FfY3Vyc29y", "cursor", ApiErrors.RequestInvalid)]
    public async Task List_WithAnInvalidParameter_Returns400OnIt(string query, string field, string code)
    {
        var userId = await NewUserAsync();

        var response = await ClientFor(userId).GetAsync($"{Transactions}?{query}", Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, code);
        Problems.AssertField(problem, field);
    }

    [Fact]
    public async Task List_WithAMalformedDate_Returns400()
    {
        var userId = await NewUserAsync();

        var response = await ClientFor(userId).GetAsync($"{Transactions}?from=yesterday", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.RequestInvalid);
    }

    [Fact]
    public async Task List_WithoutSession_Returns401()
    {
        var response = await AnonymousClient().GetAsync(Transactions, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Fact]
    public async Task List_ContainsOnlyTheTransactionsOfTheUser_EvenWhenFilteringOnAnotherUsersAccount()
    {
        var (userId, account) = await UserWithAccountAsync();
        var (_, otherAccount) = await UserWithAccountAsync();
        var mine = await Database.CreateExpenseAsync(account, cancellationToken: Ct);
        await Database.CreateExpenseAsync(otherAccount, cancellationToken: Ct);
        var client = ClientFor(userId);

        var all = await client.GetJsonAsync<PageResponse<TransactionResponse>>(Transactions);
        var other = await client.GetJsonAsync<PageResponse<TransactionResponse>>($"{Transactions}?accountId={otherAccount.Id}");

        Assert.Equal(new[] { mine.Id }, all.Items.Select(item => item.Id));
        Assert.Empty(other.Items);
    }

    // ---- Get ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Get_ReturnsTheTransactionWithItsTagsAndETag()
    {
        var (userId, account) = await UserWithAccountAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var work = await Database.CreateTagAsync(userId, "lavoro", Ct);
        var holiday = await Database.CreateTagAsync(userId, "Vacanza", Ct);
        var expense = await Database.CreateExpenseAsync(
            account, 42.90m, Day2, category, "Treno", [holiday, work], Ct);

        var response = await ClientFor(userId).GetAsync($"{Transactions}/{expense.Id}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<TransactionResponse>();
        Assert.Equal(expense.Id, body.Id);
        Assert.Equal((TransactionKind.Expense, account.Id, category.Id, Day2, 42.90m, -42.90m, "Treno"),
            (body.Kind, body.AccountId, body.CategoryId!.Value, body.Date, body.Amount, body.SignedAmount, body.Description));
        Assert.Equal(new[] { "lavoro", "Vacanza" }, body.Tags);
        Assert.Equal(EntityTag.Format(body.Version), response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task Get_WithoutSession_Returns401()
    {
        var (_, account) = await UserWithAccountAsync();
        var expense = await Database.CreateExpenseAsync(account, cancellationToken: Ct);

        var response = await AnonymousClient().GetAsync($"{Transactions}/{expense.Id}", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Fact]
    public async Task Get_AnotherUsersTransaction_Returns404()
    {
        var (_, otherAccount) = await UserWithAccountAsync();
        var other = await Database.CreateExpenseAsync(otherAccount, cancellationToken: Ct);

        var response = await ClientFor(await NewUserAsync()).GetAsync($"{Transactions}/{other.Id}", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
    }

    // ---- Create ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_AnExpense_Returns201_AndLowersTheBalance()
    {
        var (userId, account) = await UserWithAccountAsync(initialBalance: 100m);
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var client = ClientFor(userId);

        var response = await client.PostAsJsonAsync(
            Transactions,
            new { kind = "expense", accountId = account.Id, categoryId = category.Id, date = "2026-11-12", amount = 12.50m, description = " Pranzo " },
            Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<TransactionResponse>();
        Assert.Equal((TransactionKind.Expense, 12.50m, -12.50m, new DateOnly(2026, 11, 12), "Pranzo"),
            (created.Kind, created.Amount, created.SignedAmount, created.Date, created.Description));
        Assert.Empty(created.Tags);
        Assert.Equal(EntityTag.Format(created.Version), response.Headers.ETag?.ToString());
        Assert.Equal($"{Transactions}/{created.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(87.50m, (await client.GetJsonAsync<AccountResponse>($"/api/accounts/{account.Id}")).Balance);
    }

    [Fact]
    public async Task Create_AnIncome_IsPositive()
    {
        var (userId, account) = await UserWithAccountAsync();
        var category = await Database.CreateCategoryAsync(userId, kind: CategoryKind.Income, cancellationToken: Ct);

        var response = await ClientFor(userId).PostAsJsonAsync(Transactions, Body(account, category, kind: "income", amount: 1500m), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1500m, (await response.ReadAsync<TransactionResponse>()).SignedAmount);
    }

    [Fact]
    public async Task Create_WithTags_ReusesExistingOnes_AndCreatesTheOthers()
    {
        var (userId, account) = await UserWithAccountAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var holiday = await Database.CreateTagAsync(userId, "Vacanza", Ct);
        var client = ClientFor(userId);
        var body = Body(account, category);
        body["tags"] = new[] { "VACANZA", "treno", "Treno " };

        var response = await client.PostAsJsonAsync(Transactions, body, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(new[] { "treno", "Vacanza" }, (await response.ReadAsync<TransactionResponse>()).Tags);
        var tags = await client.GetJsonAsync<ListResponse<TagResponse>>("/api/tags");
        Assert.Equal(2, tags.Items.Count);
        Assert.Contains(tags.Items, tag => tag.Id == holiday.Id && tag.TransactionCount == 1);
    }

    [Fact]
    public async Task Create_SentAgainWithTheSameId_ReturnsTheFirstTransaction_AndCreatesNothing()
    {
        var (userId, account) = await UserWithAccountAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var client = ClientFor(userId);
        var body = Body(account, category);
        var id = Guid.CreateVersion7();
        body["id"] = id;

        var first = await client.PostAsJsonAsync(Transactions, body, Ct);
        var again = await client.PostAsJsonAsync(Transactions, body, Ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var created = await first.ReadAsync<TransactionResponse>();
        Assert.Equal(id, created.Id);
        Assert.Equal(created, await again.ReadAsync<TransactionResponse>());
        Assert.Single((await client.GetJsonAsync<PageResponse<TransactionResponse>>(Transactions)).Items);
    }

    [Fact]
    public async Task Create_WithAnIdOfAnotherUsersTransaction_Returns409_AndRevealsNothing()
    {
        var (_, otherAccount) = await UserWithAccountAsync();
        var other = await Database.CreateExpenseAsync(otherAccount, cancellationToken: Ct);
        var (userId, account) = await UserWithAccountAsync();
        var body = Body(account, await Database.CreateCategoryAsync(userId, cancellationToken: Ct));
        body["id"] = other.Id;

        var response = await ClientFor(userId).PostAsJsonAsync(Transactions, body, Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.Conflict, ApiErrors.Conflict);
        Problems.AssertField(problem, "id");
    }

    [Fact]
    public async Task Create_WithAnIdThatIsNotAVersion7Uuid_Returns400OnId()
    {
        var (userId, account) = await UserWithAccountAsync();
        var body = Body(account, await Database.CreateCategoryAsync(userId, cancellationToken: Ct));
        body["id"] = Guid.NewGuid();

        var response = await ClientFor(userId).PostAsJsonAsync(Transactions, body, Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed);
        Problems.AssertField(problem, "id");
    }

    [Fact]
    public async Task Create_ATransfer_Returns400OnKind()
    {
        var (userId, account) = await UserWithAccountAsync();
        var body = Body(account, await Database.CreateCategoryAsync(userId, cancellationToken: Ct), kind: "transfer");

        var response = await ClientFor(userId).PostAsJsonAsync(Transactions, body, Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed);
        Problems.AssertField(problem, "kind");
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("accountId")]
    [InlineData("categoryId")]
    [InlineData("date")]
    [InlineData("amount")]
    public async Task Create_WithoutARequiredField_Returns400OnThatField(string field)
    {
        var (userId, account) = await UserWithAccountAsync();
        var body = Body(account, await Database.CreateCategoryAsync(userId, cancellationToken: Ct));
        body.Remove(field);

        var response = await ClientFor(userId).PostAsJsonAsync(Transactions, body, Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed);
        Problems.AssertField(problem, field);
    }

    [Fact]
    public async Task Create_WithTooManyTagsOrATooLongDescription_Returns400OnTheField()
    {
        var (userId, account) = await UserWithAccountAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var client = ClientFor(userId);
        var tooManyTags = Body(account, category);
        tooManyTags["tags"] = Enumerable.Range(0, TagNames.MaxPerTransaction + 1).Select(i => $"tag-{i}").ToArray();
        var tooLong = Body(account, category);
        tooLong["description"] = new string('a', Transaction.DescriptionMaxLength + 1);

        Problems.AssertField(await Problems.AssertAsync(await client.PostAsJsonAsync(Transactions, tooManyTags, Ct), HttpStatusCode.BadRequest, ApiErrors.ValidationFailed), "tags");
        Problems.AssertField(await Problems.AssertAsync(await client.PostAsJsonAsync(Transactions, tooLong, Ct), HttpStatusCode.BadRequest, ApiErrors.ValidationFailed), "description");
    }

    [Theory]
    [InlineData(0, DomainErrors.AmountNotPositive)]
    [InlineData(-5, DomainErrors.AmountNotPositive)]
    [InlineData(1.005, DomainErrors.AmountTooManyDecimals)]
    public async Task Create_WithAnInvalidAmount_Returns400WithTheDomainCode(double amount, string code)
    {
        var (userId, account) = await UserWithAccountAsync();
        var body = Body(account, await Database.CreateCategoryAsync(userId, cancellationToken: Ct), amount: (decimal)amount);

        var response = await ClientFor(userId).PostAsJsonAsync(Transactions, body, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Create_WithACategoryOfTheOtherKind_Returns400()
    {
        var (userId, account) = await UserWithAccountAsync();
        var salary = await Database.CreateCategoryAsync(userId, kind: CategoryKind.Income, cancellationToken: Ct);

        var response = await ClientFor(userId).PostAsJsonAsync(Transactions, Body(account, salary, kind: "expense"), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, DomainErrors.CategoryKindMismatch);
    }

    [Fact]
    public async Task Create_OnAnArchivedAccountOrCategory_Returns409()
    {
        var (userId, account) = await UserWithAccountAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var archivedAccount = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var archivedCategory = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var client = ClientFor(userId);
        await client.PostEmptyAsync($"/api/accounts/{archivedAccount.Id}/archive");
        await client.PostEmptyAsync($"/api/categories/{archivedCategory.Id}/archive");

        var onAccount = await client.PostAsJsonAsync(Transactions, Body(archivedAccount, category), Ct);
        var onCategory = await client.PostAsJsonAsync(Transactions, Body(account, archivedCategory), Ct);

        await Problems.AssertAsync(onAccount, HttpStatusCode.Conflict, DomainErrors.AccountArchived);
        await Problems.AssertAsync(onCategory, HttpStatusCode.Conflict, DomainErrors.CategoryArchived);
    }

    [Fact]
    public async Task Create_WithAnotherUsersAccountOrCategory_Returns400OnTheField()
    {
        var (userId, account) = await UserWithAccountAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var (otherUserId, otherAccount) = await UserWithAccountAsync();
        var otherCategory = await Database.CreateCategoryAsync(otherUserId, cancellationToken: Ct);
        var client = ClientFor(userId);

        var onAccount = await client.PostAsJsonAsync(Transactions, Body(otherAccount, category), Ct);
        var onCategory = await client.PostAsJsonAsync(Transactions, Body(account, otherCategory), Ct);

        Problems.AssertField(await Problems.AssertAsync(onAccount, HttpStatusCode.BadRequest, "account.not_found"), "accountId");
        Problems.AssertField(await Problems.AssertAsync(onCategory, HttpStatusCode.BadRequest, "category.not_found"), "categoryId");
    }

    [Fact]
    public async Task Create_WithoutSession_Returns401()
    {
        var (userId, account) = await UserWithAccountAsync();
        var body = Body(account, await Database.CreateCategoryAsync(userId, cancellationToken: Ct));

        var response = await AnonymousClient().PostAsJsonAsync(Transactions, body, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Update ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Update_ReplacesEveryField_AndTheTags_ButNotTheKind()
    {
        var (userId, account) = await UserWithAccountAsync();
        var otherAccount = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var newCategory = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var old = await Database.CreateTagAsync(userId, "vecchio", Ct);
        var client = ClientFor(userId);
        var expense = await ReadAsync(client, (await Database.CreateExpenseAsync(account, 10m, Day1, tags: [old], cancellationToken: Ct)).Id);

        var response = await client.PutWithVersionAsync(
            $"{Transactions}/{expense.Id}",
            new { kind = "income", accountId = otherAccount.Id, categoryId = newCategory.Id, date = "2026-11-03", amount = 25m, description = "Cena", tags = new[] { "nuovo" } },
            expense.Version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<TransactionResponse>();
        Assert.Equal((TransactionKind.Expense, otherAccount.Id, newCategory.Id, Day3, 25m, -25m, "Cena"),
            (updated.Kind, updated.AccountId, updated.CategoryId!.Value, updated.Date, updated.Amount, updated.SignedAmount, updated.Description));
        Assert.Equal(new[] { "nuovo" }, updated.Tags);
        Assert.NotEqual(expense.Version, updated.Version);
        Assert.Equal(EntityTag.Format(updated.Version), response.Headers.ETag?.ToString());
        Assert.Equal(updated, await ReadAsync(client, expense.Id));
    }

    [Fact]
    public async Task Update_OfTheTagsOnly_ChangesTheVersion()
    {
        var (userId, account) = await UserWithAccountAsync();
        var client = ClientFor(userId);
        var created = await Database.CreateExpenseAsync(account, cancellationToken: Ct);
        var expense = await ReadAsync(client, created.Id);
        var body = UpdateBody(expense);
        body["tags"] = new[] { "aggiunto" };

        var tagged = await client.PutWithVersionAsync($"{Transactions}/{expense.Id}", body, expense.Version);
        body["tags"] = Array.Empty<string>();
        var stale = await client.PutWithVersionAsync($"{Transactions}/{expense.Id}", body, expense.Version);

        Assert.Equal(HttpStatusCode.OK, tagged.StatusCode);
        Assert.NotEqual(expense.Version, (await tagged.ReadAsync<TransactionResponse>()).Version);
        await Problems.AssertAsync(stale, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Equal(new[] { "aggiunto" }, (await ReadAsync(client, expense.Id)).Tags);
    }

    [Fact]
    public async Task Update_OfATransferLeg_Returns409()
    {
        var (userId, account) = await UserWithAccountAsync();
        var savings = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var transfer = await Database.CreateTransferAsync(account, savings, cancellationToken: Ct);
        var client = ClientFor(userId);
        var leg = await ReadAsync(client, transfer.Outgoing.Id);
        var body = UpdateBody(leg);
        body["categoryId"] = (await Database.CreateCategoryAsync(userId, cancellationToken: Ct)).Id;

        var response = await client.PutWithVersionAsync($"{Transactions}/{leg.Id}", body, leg.Version);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, DomainErrors.TransactionIsTransferLeg);
    }

    [Fact]
    public async Task Update_WithAMissingAmount_Returns400OnAmount()
    {
        var (userId, account) = await UserWithAccountAsync();
        var client = ClientFor(userId);
        var expense = await ReadAsync(client, (await Database.CreateExpenseAsync(account, cancellationToken: Ct)).Id);
        var body = UpdateBody(expense);
        body.Remove("amount");

        var response = await client.PutWithVersionAsync($"{Transactions}/{expense.Id}", body, expense.Version);

        Problems.AssertField(await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed), "amount");
    }

    [Fact]
    public async Task Update_ToAnotherUsersAccount_Returns400OnAccountId()
    {
        var (userId, account) = await UserWithAccountAsync();
        var (_, otherAccount) = await UserWithAccountAsync();
        var client = ClientFor(userId);
        var expense = await ReadAsync(client, (await Database.CreateExpenseAsync(account, cancellationToken: Ct)).Id);
        var body = UpdateBody(expense);
        body["accountId"] = otherAccount.Id;

        var response = await client.PutWithVersionAsync($"{Transactions}/{expense.Id}", body, expense.Version);

        Problems.AssertField(await Problems.AssertAsync(response, HttpStatusCode.BadRequest, "account.not_found"), "accountId");
    }

    [Fact]
    public async Task Update_WithoutIfMatch_Returns428()
    {
        var (userId, account) = await UserWithAccountAsync();
        var client = ClientFor(userId);
        var expense = await ReadAsync(client, (await Database.CreateExpenseAsync(account, cancellationToken: Ct)).Id);

        var response = await client.PutAsJsonAsync($"{Transactions}/{expense.Id}", UpdateBody(expense), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionRequired, ApiErrors.VersionRequired);
    }

    [Fact]
    public async Task Update_WithAStaleVersion_Returns412_AndChangesNothing()
    {
        var (userId, account) = await UserWithAccountAsync();
        var client = ClientFor(userId);
        var expense = await ReadAsync(client, (await Database.CreateExpenseAsync(account, 10m, cancellationToken: Ct)).Id);
        var body = UpdateBody(expense);
        body["amount"] = 99m;

        var response = await client.PutWithVersionAsync($"{Transactions}/{expense.Id}", body, expense.Version + 1);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Equal(10m, (await ReadAsync(client, expense.Id)).Amount);
    }

    [Fact]
    public async Task Update_AnotherUsersTransaction_Returns404()
    {
        var (otherUserId, otherAccount) = await UserWithAccountAsync();
        var other = await ReadAsync(ClientFor(otherUserId), (await Database.CreateExpenseAsync(otherAccount, cancellationToken: Ct)).Id);

        var response = await ClientFor(await NewUserAsync()).PutWithVersionAsync($"{Transactions}/{other.Id}", UpdateBody(other), other.Version);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
    }

    [Fact]
    public async Task Update_WithoutSession_Returns401()
    {
        var (userId, account) = await UserWithAccountAsync();
        var expense = await ReadAsync(ClientFor(userId), (await Database.CreateExpenseAsync(account, cancellationToken: Ct)).Id);

        var response = await AnonymousClient().PutWithVersionAsync($"{Transactions}/{expense.Id}", UpdateBody(expense), expense.Version);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Delete ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_Returns204_RemovesIt_AndKeepsItsTags()
    {
        var (userId, account) = await UserWithAccountAsync(initialBalance: 100m);
        var holiday = await Database.CreateTagAsync(userId, cancellationToken: Ct);
        var client = ClientFor(userId);
        var expense = await ReadAsync(client, (await Database.CreateExpenseAsync(account, 30m, tags: [holiday], cancellationToken: Ct)).Id);

        var response = await client.DeleteWithVersionAsync($"{Transactions}/{expense.Id}", expense.Version);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Transactions}/{expense.Id}", Ct)).StatusCode);
        Assert.Equal(100m, (await client.GetJsonAsync<AccountResponse>($"/api/accounts/{account.Id}")).Balance);
        Assert.Contains((await client.GetJsonAsync<ListResponse<TagResponse>>("/api/tags")).Items, tag => tag.Id == holiday.Id);
    }

    [Fact]
    public async Task Delete_ATransferLeg_Returns409_AndKeepsBothLegs()
    {
        var (userId, account) = await UserWithAccountAsync();
        var savings = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var transfer = await Database.CreateTransferAsync(account, savings, cancellationToken: Ct);
        var client = ClientFor(userId);
        var leg = await ReadAsync(client, transfer.Incoming.Id);

        var response = await client.DeleteWithVersionAsync($"{Transactions}/{leg.Id}", leg.Version);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, DomainErrors.TransactionIsTransferLeg);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Transactions}/{transfer.Outgoing.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutIfMatch_Returns428()
    {
        var (userId, account) = await UserWithAccountAsync();
        var expense = await Database.CreateExpenseAsync(account, cancellationToken: Ct);

        var response = await ClientFor(userId).DeleteAsync($"{Transactions}/{expense.Id}", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionRequired, ApiErrors.VersionRequired);
    }

    [Fact]
    public async Task Delete_WithAStaleVersion_Returns412_AndKeepsIt()
    {
        var (userId, account) = await UserWithAccountAsync();
        var client = ClientFor(userId);
        var expense = await ReadAsync(client, (await Database.CreateExpenseAsync(account, cancellationToken: Ct)).Id);

        var response = await client.DeleteWithVersionAsync($"{Transactions}/{expense.Id}", expense.Version + 1);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Transactions}/{expense.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_AnotherUsersTransaction_Returns404_AndKeepsIt()
    {
        var (otherUserId, otherAccount) = await UserWithAccountAsync();
        var other = await ReadAsync(ClientFor(otherUserId), (await Database.CreateExpenseAsync(otherAccount, cancellationToken: Ct)).Id);

        var response = await ClientFor(await NewUserAsync()).DeleteWithVersionAsync($"{Transactions}/{other.Id}", other.Version);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
        Assert.Equal(HttpStatusCode.OK, (await ClientFor(otherUserId).GetAsync($"{Transactions}/{other.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutSession_Returns401()
    {
        var (_, account) = await UserWithAccountAsync();
        var expense = await Database.CreateExpenseAsync(account, cancellationToken: Ct);

        var response = await AnonymousClient().DeleteWithVersionAsync($"{Transactions}/{expense.Id}", 1);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Helpers -----------------------------------------------------------------------------------------------

    private async Task<(Guid UserId, Account Account)> UserWithAccountAsync(decimal initialBalance = 0m)
    {
        var userId = await NewUserAsync();
        return (userId, await Database.CreateAccountAsync(userId, initialBalance: initialBalance, cancellationToken: Ct));
    }

    /// <summary>A valid create body; tests remove or replace one field.</summary>
    private static Dictionary<string, object?> Body(Account account, Category category, string kind = "expense", decimal amount = 10m) => new()
    {
        ["kind"] = kind,
        ["accountId"] = account.Id,
        ["categoryId"] = category.Id,
        ["date"] = "2026-11-12",
        ["amount"] = amount,
    };

    /// <summary>The update body of <paramref name="transaction"/> as read, like a form that changes nothing yet.</summary>
    private static Dictionary<string, object?> UpdateBody(TransactionResponse transaction) => new()
    {
        ["accountId"] = transaction.AccountId,
        ["categoryId"] = transaction.CategoryId,
        ["date"] = transaction.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        ["amount"] = transaction.Amount,
        ["description"] = transaction.Description,
        ["tags"] = transaction.Tags,
    };

    private static Task<TransactionResponse> ReadAsync(HttpClient client, Guid id) =>
        client.GetJsonAsync<TransactionResponse>($"{Transactions}/{id}");
}
