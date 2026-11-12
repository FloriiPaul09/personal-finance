using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Json;
using PersonalFinance.Api.Common.Responses;
using PersonalFinance.Api.Features.Accounts;
using PersonalFinance.Api.Tests.Support;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Common;
using PersonalFinance.Testing;

namespace PersonalFinance.Api.Tests.Features.Accounts;

// Module accounts (docs/backend/api-endpoints.md#accounts): every endpoint with success, 401, validation, isolation
// and, for writes, versions (docs/backend/testing.md#minimum-per-endpoint).
public sealed class AccountsTests(ApiServer server) : ApiTest(server)
{
    private const string Accounts = "/api/accounts";

    // ---- List --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task List_ReturnsTheActiveAccountsByName_InAnItemsEnvelope()
    {
        var userId = await NewUserAsync();
        var savings = await Database.CreateAccountAsync(userId, name: "risparmi", initialBalance: 1000m, cancellationToken: Ct);
        var cash = await Database.CreateAccountAsync(userId, name: "Contanti", type: AccountType.Cash, initialBalance: 50m, cancellationToken: Ct);
        var archived = await Database.CreateAccountAsync(userId, name: "Archiviato", cancellationToken: Ct);
        await ArchiveAsync(userId, archived);

        var response = await ClientFor(userId).GetAsync(Accounts, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.GetProperty("items").ValueKind);

        var items = body.Deserialize<ListResponse<AccountResponse>>(JsonConventions.Options)!.Items;
        Assert.Equal(new[] { cash.Id, savings.Id }, items.Select(account => account.Id));
        Assert.Equal(50m, items[0].Balance);
        Assert.All(items, account => Assert.NotEqual(0u, account.Version));
    }

    [Fact]
    public async Task List_WithIncludeArchived_AddsTheArchivedAccountsAfterTheActiveOnes()
    {
        var userId = await NewUserAsync();
        var archived = await Database.CreateAccountAsync(userId, name: "A archiviato", cancellationToken: Ct);
        var active = await Database.CreateAccountAsync(userId, name: "B attivo", cancellationToken: Ct);
        await ArchiveAsync(userId, archived);

        var list = await ClientFor(userId).GetJsonAsync<ListResponse<AccountResponse>>($"{Accounts}?includeArchived=true");

        Assert.Equal(new[] { active.Id, archived.Id }, list.Items.Select(account => account.Id));
        Assert.True(list.Items[1].IsArchived);
    }

    [Fact]
    public async Task List_WithoutSession_Returns401()
    {
        var response = await AnonymousClient().GetAsync(Accounts, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Fact]
    public async Task List_WithAMalformedFilter_Returns400()
    {
        var userId = await NewUserAsync();

        var response = await ClientFor(userId).GetAsync($"{Accounts}?includeArchived=maybe", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.RequestInvalid);
    }

    [Fact]
    public async Task List_ContainsOnlyTheAccountsOfTheUser()
    {
        var userId = await NewUserAsync();
        var otherUserId = await NewUserAsync();
        var mine = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        await Database.CreateAccountAsync(otherUserId, cancellationToken: Ct);

        var list = await ClientFor(userId).GetJsonAsync<ListResponse<AccountResponse>>($"{Accounts}?includeArchived=true");

        Assert.Equal(new[] { mine.Id }, list.Items.Select(account => account.Id));
    }

    // ---- Get ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Get_ReturnsTheAccountWithItsVersionAsETag()
    {
        var userId = await NewUserAsync();
        var account = await Database.CreateAccountAsync(
            userId, name: "Carta", type: AccountType.CreditCard, currency: "USD", initialBalance: -120.50m, cancellationToken: Ct);

        var response = await ClientFor(userId).GetAsync($"{Accounts}/{account.Id}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<AccountResponse>();
        Assert.Equal(new AccountResponse(account.Id, "Carta", AccountType.CreditCard, "USD", -120.50m, -120.50m, false, false, body.Version), body);
        Assert.Equal(EntityTag.Format(body.Version), response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task Get_SendsEnumerationsAsCamelCaseStrings()
    {
        var userId = await NewUserAsync();
        var account = await Database.CreateAccountAsync(userId, type: AccountType.CreditCard, cancellationToken: Ct);

        var body = await ClientFor(userId).GetJsonAsync<JsonElement>($"{Accounts}/{account.Id}");

        Assert.Equal("creditCard", body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Get_Balance_AddsEveryMovement_AndEqualsTheDomainBalance()
    {
        var userId = await NewUserAsync();
        var checking = await Database.CreateAccountAsync(userId, initialBalance: 100m, cancellationToken: Ct);
        var savings = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        await Database.CreateIncomeAsync(checking, 1500m, cancellationToken: Ct);
        await Database.CreateExpenseAsync(checking, 80.25m, cancellationToken: Ct);
        await Database.CreateTransferAsync(checking, savings, 200m, cancellationToken: Ct);
        await Database.CreateTransferAsync(savings, checking, 20.10m, cancellationToken: Ct);

        var body = await ClientFor(userId).GetJsonAsync<AccountResponse>($"{Accounts}/{checking.Id}");

        await using var db = Database.CreateContext(userId);
        var movements = await db.Transactions.Where(transaction => transaction.AccountId == checking.Id).ToListAsync(Ct);
        Assert.Equal(1339.85m, body.Balance);
        Assert.Equal(checking.BalanceAt(movements), body.Balance);
        Assert.True(body.HasMovements);
    }

    [Fact]
    public async Task Get_WithoutSession_Returns401()
    {
        var userId = await NewUserAsync();
        var account = await Database.CreateAccountAsync(userId, cancellationToken: Ct);

        var response = await AnonymousClient().GetAsync($"{Accounts}/{account.Id}", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Fact]
    public async Task Get_AnotherUsersAccount_Returns404_LikeANonexistentOne()
    {
        var userId = await NewUserAsync();
        var otherAccount = await Database.CreateAccountAsync(await NewUserAsync(), cancellationToken: Ct);
        var client = ClientFor(userId);

        await Problems.AssertAsync(await client.GetAsync($"{Accounts}/{otherAccount.Id}", Ct), HttpStatusCode.NotFound, ApiErrors.NotFound);
        await Problems.AssertAsync(await client.GetAsync($"{Accounts}/{Guid.CreateVersion7()}", Ct), HttpStatusCode.NotFound, ApiErrors.NotFound);
    }

    // ---- Create ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_Returns201WithLocationAndETag()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);

        var response = await client.PostAsJsonAsync(
            Accounts, new { name = "  Conto corrente ", type = "checking", currency = "eur", initialBalance = 250.10m }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<AccountResponse>();
        Assert.Equal("Conto corrente", created.Name);
        Assert.Equal(AccountType.Checking, created.Type);
        Assert.Equal("EUR", created.Currency);
        Assert.Equal(250.10m, created.InitialBalance);
        Assert.Equal(250.10m, created.Balance);
        Assert.False(created.IsArchived);
        Assert.False(created.HasMovements);
        Assert.Equal(EntityTag.Format(created.Version), response.Headers.ETag?.ToString());
        Assert.Equal($"{Accounts}/{created.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(created, await client.GetJsonAsync<AccountResponse>(response.Headers.Location!.OriginalString));
    }

    [Fact]
    public async Task Create_WithoutSession_Returns401()
    {
        var response = await AnonymousClient().PostAsJsonAsync(Accounts, ValidCreate(), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("type")]
    [InlineData("currency")]
    [InlineData("initialBalance")]
    public async Task Create_WithoutARequiredField_Returns400OnThatField(string field)
    {
        var userId = await NewUserAsync();
        var body = ValidCreate();
        body.Remove(field);

        var response = await ClientFor(userId).PostAsJsonAsync(Accounts, body, Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed);
        Problems.AssertField(problem, field);
    }

    [Theory]
    [InlineData("name", "   ")]
    [InlineData("currency", "EU")]
    [InlineData("currency", "EURO")]
    public async Task Create_WithAnInvalidField_Returns400OnThatField(string field, string value)
    {
        var userId = await NewUserAsync();
        var body = ValidCreate();
        body[field] = value;

        var response = await ClientFor(userId).PostAsJsonAsync(Accounts, body, Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed);
        Problems.AssertField(problem, field);
    }

    [Fact]
    public async Task Create_WithANameTooLong_Returns400OnName()
    {
        var userId = await NewUserAsync();
        var body = ValidCreate();
        body["name"] = new string('a', Account.NameMaxLength + 1);

        var response = await ClientFor(userId).PostAsJsonAsync(Accounts, body, Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed);
        Problems.AssertField(problem, "name");
    }

    [Theory]
    [InlineData("\"bank\"")]
    [InlineData("1")]
    public async Task Create_WithAnUnknownType_Returns400RequestInvalid(string type)
    {
        var userId = await NewUserAsync();
        var json = $$"""{ "name": "Conto", "type": {{type}}, "currency": "EUR", "initialBalance": 0 }""";

        var response = await ClientFor(userId).PostAsync(Accounts, new StringContent(json, System.Text.Encoding.UTF8, "application/json"), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.RequestInvalid);
    }

    [Theory]
    [InlineData("currency", "E1R", DomainErrors.CurrencyInvalid)]
    [InlineData("initialBalance", 10.005, DomainErrors.AmountTooManyDecimals)]
    public async Task Create_ValueRejectedByTheDomain_Returns400WithItsCode(string field, object value, string code)
    {
        var userId = await NewUserAsync();
        var body = ValidCreate();
        body[field] = value is double number ? (decimal)number : value;

        var response = await ClientFor(userId).PostAsJsonAsync(Accounts, body, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Create_WithTheNameOfAnotherAccount_Returns409_EvenIfArchivedOrInAnotherCase()
    {
        var userId = await NewUserAsync();
        var archived = await Database.CreateAccountAsync(userId, name: "Conto Corrente", cancellationToken: Ct);
        await ArchiveAsync(userId, archived);
        var body = ValidCreate();
        body["name"] = "conto CORRENTE";

        var response = await ClientFor(userId).PostAsJsonAsync(Accounts, body, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, ApiErrors.AccountNameTaken);
    }

    [Fact]
    public async Task Create_WithTheNameOfAnotherUsersAccount_Succeeds()
    {
        var userId = await NewUserAsync();
        var other = await Database.CreateAccountAsync(await NewUserAsync(), cancellationToken: Ct);
        var body = ValidCreate();
        body["name"] = other.Name;

        var response = await ClientFor(userId).PostAsJsonAsync(Accounts, body, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Create_WithMalformedJson_Returns400RequestInvalid_InEveryEnvironment(string environment)
    {
        var userId = await NewUserAsync();
        await using var production = environment == Environments.Production ? new ApiFactory(Database, environment) : null;
        var client = production?.CreateApiClient(userId) ?? ClientFor(userId);

        var response = await client.PostAsync(Accounts, new StringContent("{ \"name\": ", System.Text.Encoding.UTF8, "application/json"), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.RequestInvalid);
    }

    // ---- Update ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Update_ReplacesTheAccount_AndReturnsTheNewVersion()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var account = await ReadAsync(client, await Database.CreateAccountAsync(userId, cancellationToken: Ct));

        var response = await client.PutWithVersionAsync(
            $"{Accounts}/{account.Id}",
            new { name = "Risparmi", type = "savings", currency = "chf", initialBalance = 10.50m },
            account.Version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<AccountResponse>();
        Assert.Equal(new AccountResponse(account.Id, "Risparmi", AccountType.Savings, "CHF", 10.50m, 10.50m, false, false, updated.Version), updated);
        Assert.NotEqual(account.Version, updated.Version);
        Assert.Equal(EntityTag.Format(updated.Version), response.Headers.ETag?.ToString());
        Assert.Equal(updated, await ReadAsync(client, account.Id));
    }

    [Fact]
    public async Task Update_WithoutSession_Returns401()
    {
        var userId = await NewUserAsync();
        var account = await Database.CreateAccountAsync(userId, cancellationToken: Ct);

        var response = await AnonymousClient().PutWithVersionAsync($"{Accounts}/{account.Id}", ValidCreate(), 1);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Fact]
    public async Task Update_WithAnInvalidField_Returns400OnThatField()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var account = await ReadAsync(client, await Database.CreateAccountAsync(userId, cancellationToken: Ct));
        var body = ValidCreate();
        body["name"] = "";

        var response = await client.PutWithVersionAsync($"{Accounts}/{account.Id}", body, account.Version);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed);
        Problems.AssertField(problem, "name");
    }

    [Fact]
    public async Task Update_WithoutIfMatch_Returns428()
    {
        var userId = await NewUserAsync();
        var account = await Database.CreateAccountAsync(userId, cancellationToken: Ct);

        var response = await ClientFor(userId).PutAsJsonAsync($"{Accounts}/{account.Id}", ValidCreate(), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionRequired, ApiErrors.VersionRequired);
    }

    [Fact]
    public async Task Update_WithAStaleVersion_Returns412_AndChangesNothing()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var account = await ReadAsync(client, await Database.CreateAccountAsync(userId, name: "Prima", cancellationToken: Ct));
        var body = ValidCreate();
        body["name"] = "Dopo";

        var response = await client.PutWithVersionAsync($"{Accounts}/{account.Id}", body, account.Version + 1);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Equal("Prima", (await ReadAsync(client, account.Id)).Name);
    }

    [Fact]
    public async Task Update_AnotherUsersAccount_Returns404()
    {
        var userId = await NewUserAsync();
        var other = await Database.CreateAccountAsync(await NewUserAsync(), cancellationToken: Ct);

        var response = await ClientFor(userId).PutWithVersionAsync($"{Accounts}/{other.Id}", ValidCreate(), 1);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
    }

    [Fact]
    public async Task Update_CurrencyOfAnAccountWithMovements_Returns409_ButOtherFieldsCanChange()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var created = await Database.CreateAccountAsync(userId, currency: "EUR", cancellationToken: Ct);
        await Database.CreateExpenseAsync(created, 10m, cancellationToken: Ct);
        var account = await ReadAsync(client, created.Id);

        var newCurrency = await client.PutWithVersionAsync(
            $"{Accounts}/{account.Id}", new { name = account.Name, type = "checking", currency = "USD", initialBalance = 0m }, account.Version);
        var newName = await client.PutWithVersionAsync(
            $"{Accounts}/{account.Id}", new { name = "Rinominato", type = "checking", currency = "eur", initialBalance = 5m }, account.Version);

        await Problems.AssertAsync(newCurrency, HttpStatusCode.Conflict, DomainErrors.AccountCurrencyLocked);
        Assert.Equal(HttpStatusCode.OK, newName.StatusCode);
        var updated = await newName.ReadAsync<AccountResponse>();
        Assert.Equal("Rinominato", updated.Name);
        Assert.Equal(-5m, updated.Balance);
    }

    [Fact]
    public async Task Update_WithTheNameOfAnotherAccount_Returns409()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        await Database.CreateAccountAsync(userId, name: "Contanti", cancellationToken: Ct);
        var account = await ReadAsync(client, await Database.CreateAccountAsync(userId, cancellationToken: Ct));
        var body = ValidCreate();
        body["name"] = "CONTANTI";

        var response = await client.PutWithVersionAsync($"{Accounts}/{account.Id}", body, account.Version);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, ApiErrors.AccountNameTaken);
    }

    // ---- Archive and unarchive ---------------------------------------------------------------------------------

    [Fact]
    public async Task Archive_HidesTheAccountFromTheList_KeepsItsBalance_AndChangesTheVersion()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var created = await Database.CreateAccountAsync(userId, initialBalance: 100m, cancellationToken: Ct);
        await Database.CreateExpenseAsync(created, 30m, cancellationToken: Ct);
        var before = await ReadAsync(client, created.Id);

        var response = await client.PostEmptyAsync($"{Accounts}/{created.Id}/archive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var archived = await response.ReadAsync<AccountResponse>();
        Assert.True(archived.IsArchived);
        Assert.Equal(70m, archived.Balance);
        Assert.NotEqual(before.Version, archived.Version);
        Assert.Equal(EntityTag.Format(archived.Version), response.Headers.ETag?.ToString());
        Assert.DoesNotContain(
            (await client.GetJsonAsync<ListResponse<AccountResponse>>(Accounts)).Items, account => account.Id == created.Id);

        // A form opened before archiving cannot overwrite the account without reading it again.
        var stale = await client.PutWithVersionAsync($"{Accounts}/{created.Id}", ValidCreate(), before.Version);
        await Problems.AssertAsync(stale, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
    }

    [Fact]
    public async Task Archive_IsIdempotent()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var account = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var first = await (await client.PostEmptyAsync($"{Accounts}/{account.Id}/archive")).ReadAsync<AccountResponse>();

        var response = await client.PostEmptyAsync($"{Accounts}/{account.Id}/archive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(first, await response.ReadAsync<AccountResponse>());
    }

    [Fact]
    public async Task Unarchive_MakesTheAccountActiveAgain()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var account = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        await ArchiveAsync(userId, account);

        var response = await client.PostEmptyAsync($"{Accounts}/{account.Id}/unarchive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.ReadAsync<AccountResponse>()).IsArchived);
        Assert.Contains((await client.GetJsonAsync<ListResponse<AccountResponse>>(Accounts)).Items, item => item.Id == account.Id);
    }

    [Theory]
    [InlineData("archive")]
    [InlineData("unarchive")]
    public async Task ArchiveActions_WithoutSession_Return401(string action)
    {
        var account = await Database.CreateAccountAsync(await NewUserAsync(), cancellationToken: Ct);

        var response = await AnonymousClient().PostEmptyAsync($"{Accounts}/{account.Id}/{action}");

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Theory]
    [InlineData("archive")]
    [InlineData("unarchive")]
    public async Task ArchiveActions_OnAnotherUsersAccount_Return404_AndChangeNothing(string action)
    {
        var otherUserId = await NewUserAsync();
        var other = await Database.CreateAccountAsync(otherUserId, cancellationToken: Ct);

        var response = await ClientFor(await NewUserAsync()).PostEmptyAsync($"{Accounts}/{other.Id}/{action}");

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
        Assert.False((await ReadAsync(ClientFor(otherUserId), other.Id)).IsArchived);
    }

    [Theory]
    [InlineData("archive")]
    [InlineData("unarchive")]
    public async Task ArchiveActions_WithoutAntiforgeryToken_Return400(string action)
    {
        var userId = await NewUserAsync();
        var account = await Database.CreateAccountAsync(userId, cancellationToken: Ct);

        // Signed in, but without the antiforgery handler: like a page of another site posting with the user's cookie.
        using var client = Host.CreateClient(new() { BaseAddress = TestClients.BaseAddress });
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.Header, userId.ToString());

        var response = await client.PostEmptyAsync($"{Accounts}/{account.Id}/{action}");

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.AntiforgeryInvalid);
    }

    // ---- Delete ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_AnAccountWithoutMovements_Returns204_AndRemovesIt()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var account = await ReadAsync(client, await Database.CreateAccountAsync(userId, cancellationToken: Ct));

        var response = await client.DeleteWithVersionAsync($"{Accounts}/{account.Id}", account.Version);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Accounts}/{account.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutSession_Returns401()
    {
        var account = await Database.CreateAccountAsync(await NewUserAsync(), cancellationToken: Ct);

        var response = await AnonymousClient().DeleteWithVersionAsync($"{Accounts}/{account.Id}", 1);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Fact]
    public async Task Delete_WithoutIfMatch_Returns428()
    {
        var userId = await NewUserAsync();
        var account = await Database.CreateAccountAsync(userId, cancellationToken: Ct);

        var response = await ClientFor(userId).DeleteAsync($"{Accounts}/{account.Id}", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionRequired, ApiErrors.VersionRequired);
    }

    [Fact]
    public async Task Delete_WithAStaleVersion_Returns412_AndKeepsTheAccount()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var account = await ReadAsync(client, await Database.CreateAccountAsync(userId, cancellationToken: Ct));

        var response = await client.DeleteWithVersionAsync($"{Accounts}/{account.Id}", account.Version + 1);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Accounts}/{account.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_AnotherUsersAccount_Returns404_AndKeepsIt()
    {
        var otherUserId = await NewUserAsync();
        var other = await ReadAsync(ClientFor(otherUserId), await Database.CreateAccountAsync(otherUserId, cancellationToken: Ct));

        var response = await ClientFor(await NewUserAsync()).DeleteWithVersionAsync($"{Accounts}/{other.Id}", other.Version);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
        Assert.Equal(HttpStatusCode.OK, (await ClientFor(otherUserId).GetAsync($"{Accounts}/{other.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_AnAccountWithMovements_Returns409_AndKeepsIt()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var created = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        await Database.CreateIncomeAsync(created, cancellationToken: Ct);
        var account = await ReadAsync(client, created.Id);

        var response = await client.DeleteWithVersionAsync($"{Accounts}/{account.Id}", account.Version);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, DomainErrors.AccountHasMovements);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Accounts}/{account.Id}", Ct)).StatusCode);
    }

    // ---- Helpers -----------------------------------------------------------------------------------------------

    /// <summary>A valid create or update body; tests remove or replace one field.</summary>
    private static Dictionary<string, object?> ValidCreate() => new()
    {
        ["name"] = TestData.UniqueName("Conto"),
        ["type"] = "checking",
        ["currency"] = "EUR",
        ["initialBalance"] = 0m,
    };

    private static Task<AccountResponse> ReadAsync(HttpClient client, Account account) => ReadAsync(client, account.Id);

    private static Task<AccountResponse> ReadAsync(HttpClient client, Guid id) =>
        client.GetJsonAsync<AccountResponse>($"{Accounts}/{id}");

    private async Task ArchiveAsync(Guid userId, Account account)
    {
        await using var db = Database.CreateContext(userId);
        var tracked = await db.Accounts.SingleAsync(item => item.Id == account.Id, Ct);
        tracked.Archive();
        await db.SaveChangesAsync(Ct);
    }
}
