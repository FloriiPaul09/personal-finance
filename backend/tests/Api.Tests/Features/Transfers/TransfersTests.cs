using System.Net;
using System.Net.Http.Json;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Responses;
using PersonalFinance.Api.Features.Accounts;
using PersonalFinance.Api.Features.Transactions;
using PersonalFinance.Api.Features.Transfers;
using PersonalFinance.Api.Tests.Support;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Common;
using PersonalFinance.Domain.Transactions;
using PersonalFinance.Testing;

namespace PersonalFinance.Api.Tests.Features.Transfers;

// Module transfers (docs/backend/api-endpoints.md#transfers): every endpoint with success, 401, validation,
// isolation and, for writes, versions (docs/backend/testing.md#minimum-per-endpoint).
public sealed class TransfersTests(ApiServer server) : ApiTest(server)
{
    private const string Transfers = "/api/transfers";

    // ---- Get ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Get_ReturnsBothSidesOfTheTransfer_WithItsVersionAsETag()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();
        var transfer = await Database.CreateTransferAsync(checking, savings, 250m, new DateOnly(2026, 11, 10), Ct);

        var response = await ClientFor(userId).GetAsync($"{Transfers}/{transfer.Id}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<TransferResponse>();
        Assert.Equal(
            new TransferResponse(
                transfer.Id, checking.Id, savings.Id, new DateOnly(2026, 11, 10), 250m, null, transfer.Outgoing.Id, transfer.Incoming.Id, body.Version),
            body);
        Assert.Equal(EntityTag.Format(body.Version), response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task Get_WithoutSession_Returns401()
    {
        var (_, checking, savings) = await UserWithTwoAccountsAsync();
        var transfer = await Database.CreateTransferAsync(checking, savings, cancellationToken: Ct);

        var response = await AnonymousClient().GetAsync($"{Transfers}/{transfer.Id}", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Fact]
    public async Task Get_AnotherUsersTransfer_Returns404()
    {
        var (_, checking, savings) = await UserWithTwoAccountsAsync();
        var other = await Database.CreateTransferAsync(checking, savings, cancellationToken: Ct);

        var response = await ClientFor(await NewUserAsync()).GetAsync($"{Transfers}/{other.Id}", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
    }

    // ---- Create ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_Returns201_MovesTheMoney_AndListsBothLegs()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync(checkingBalance: 1000m);
        var client = ClientFor(userId);

        var response = await client.PostAsJsonAsync(
            Transfers,
            new { fromAccountId = checking.Id, toAccountId = savings.Id, date = "2026-11-12", amount = 300m, description = " Risparmio " },
            Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<TransferResponse>();
        Assert.Equal((checking.Id, savings.Id, new DateOnly(2026, 11, 12), 300m, "Risparmio"),
            (created.FromAccountId, created.ToAccountId, created.Date, created.Amount, created.Description));
        Assert.Equal(EntityTag.Format(created.Version), response.Headers.ETag?.ToString());
        Assert.Equal($"{Transfers}/{created.Id}", response.Headers.Location?.OriginalString);

        Assert.Equal(700m, (await client.GetJsonAsync<AccountResponse>($"/api/accounts/{checking.Id}")).Balance);
        Assert.Equal(300m, (await client.GetJsonAsync<AccountResponse>($"/api/accounts/{savings.Id}")).Balance);

        var legs = (await client.GetJsonAsync<PageResponse<TransactionResponse>>("/api/transactions?kind=transfer")).Items;
        Assert.Equal(
            new[] { (created.OutgoingTransactionId, -300m), (created.IncomingTransactionId, 300m) }.Order(),
            legs.Select(leg => (leg.Id, leg.SignedAmount)).Order());
        Assert.All(legs, leg => Assert.Equal(created.Id, leg.TransferId));
    }

    [Fact]
    public async Task Create_SentAgainWithTheSameId_ReturnsTheFirstTransfer_AndMovesTheMoneyOnce()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync(checkingBalance: 1000m);
        var client = ClientFor(userId);
        var body = Body(checking, savings, amount: 100m);
        var id = Guid.CreateVersion7();
        body["id"] = id;

        var first = await client.PostAsJsonAsync(Transfers, body, Ct);
        var again = await client.PostAsJsonAsync(Transfers, body, Ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var created = await first.ReadAsync<TransferResponse>();
        Assert.Equal(id, created.Id);
        Assert.Equal(created, await again.ReadAsync<TransferResponse>());
        Assert.Equal(900m, (await client.GetJsonAsync<AccountResponse>($"/api/accounts/{checking.Id}")).Balance);
    }

    [Fact]
    public async Task Create_WithAnIdOfAnotherUsersTransfer_Returns409OnId()
    {
        var (_, otherChecking, otherSavings) = await UserWithTwoAccountsAsync();
        var other = await Database.CreateTransferAsync(otherChecking, otherSavings, cancellationToken: Ct);
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();
        var body = Body(checking, savings);
        body["id"] = other.Id;

        var response = await ClientFor(userId).PostAsJsonAsync(Transfers, body, Ct);

        Problems.AssertField(await Problems.AssertAsync(response, HttpStatusCode.Conflict, ApiErrors.Conflict), "id");
    }

    [Fact]
    public async Task Create_WithAnIdThatIsNotAVersion7Uuid_Returns400OnId()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();
        var body = Body(checking, savings);
        body["id"] = Guid.NewGuid();

        var response = await ClientFor(userId).PostAsJsonAsync(Transfers, body, Ct);

        Problems.AssertField(await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed), "id");
    }

    [Theory]
    [InlineData("fromAccountId")]
    [InlineData("toAccountId")]
    [InlineData("date")]
    [InlineData("amount")]
    public async Task Create_WithoutARequiredField_Returns400OnThatField(string field)
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();
        var body = Body(checking, savings);
        body.Remove(field);

        var response = await ClientFor(userId).PostAsJsonAsync(Transfers, body, Ct);

        Problems.AssertField(await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed), field);
    }

    [Theory]
    [InlineData(0, DomainErrors.AmountNotPositive)]
    [InlineData(10.001, DomainErrors.AmountTooManyDecimals)]
    public async Task Create_WithAnInvalidAmount_Returns400WithTheDomainCode(double amount, string code)
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();

        var response = await ClientFor(userId).PostAsJsonAsync(Transfers, Body(checking, savings, amount: (decimal)amount), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Create_ToTheSameAccount_Returns400()
    {
        var (userId, checking, _) = await UserWithTwoAccountsAsync();

        var response = await ClientFor(userId).PostAsJsonAsync(Transfers, Body(checking, checking), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, DomainErrors.TransferSameAccount);
    }

    [Fact]
    public async Task Create_BetweenCurrencies_Returns400()
    {
        var (userId, checking, _) = await UserWithTwoAccountsAsync();
        var dollars = await Database.CreateAccountAsync(userId, currency: "USD", cancellationToken: Ct);

        var response = await ClientFor(userId).PostAsJsonAsync(Transfers, Body(checking, dollars), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, DomainErrors.TransferCurrencyMismatch);
    }

    [Fact]
    public async Task Create_FromOrToAnArchivedAccount_Returns409()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();
        var archived = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var client = ClientFor(userId);
        await client.PostEmptyAsync($"/api/accounts/{archived.Id}/archive");

        var fromArchived = await client.PostAsJsonAsync(Transfers, Body(archived, savings), Ct);
        var toArchived = await client.PostAsJsonAsync(Transfers, Body(checking, archived), Ct);

        await Problems.AssertAsync(fromArchived, HttpStatusCode.Conflict, DomainErrors.AccountArchived);
        await Problems.AssertAsync(toArchived, HttpStatusCode.Conflict, DomainErrors.AccountArchived);
    }

    [Fact]
    public async Task Create_WithAnotherUsersAccount_Returns400OnTheField()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();
        var (_, otherAccount, _) = await UserWithTwoAccountsAsync();
        var client = ClientFor(userId);

        var from = await client.PostAsJsonAsync(Transfers, Body(otherAccount, savings), Ct);
        var to = await client.PostAsJsonAsync(Transfers, Body(checking, otherAccount), Ct);

        Problems.AssertField(await Problems.AssertAsync(from, HttpStatusCode.BadRequest, "account.not_found"), "fromAccountId");
        Problems.AssertField(await Problems.AssertAsync(to, HttpStatusCode.BadRequest, "account.not_found"), "toAccountId");
    }

    [Fact]
    public async Task Create_WithoutSession_Returns401()
    {
        var (_, checking, savings) = await UserWithTwoAccountsAsync();

        var response = await AnonymousClient().PostAsJsonAsync(Transfers, Body(checking, savings), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Update ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Update_ChangesBothLegs_AndTheVersion()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync(checkingBalance: 1000m);
        var cash = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var client = ClientFor(userId);
        var transfer = await ReadAsync(client, (await Database.CreateTransferAsync(checking, savings, 100m, cancellationToken: Ct)).Id);

        var response = await client.PutWithVersionAsync(
            $"{Transfers}/{transfer.Id}",
            new { fromAccountId = checking.Id, toAccountId = cash.Id, date = "2026-11-05", amount = 40m, description = "Prelievo" },
            transfer.Version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<TransferResponse>();
        Assert.Equal(
            new TransferResponse(
                transfer.Id, checking.Id, cash.Id, new DateOnly(2026, 11, 5), 40m, "Prelievo", transfer.OutgoingTransactionId, transfer.IncomingTransactionId, updated.Version),
            updated);
        Assert.NotEqual(transfer.Version, updated.Version);
        Assert.Equal(EntityTag.Format(updated.Version), response.Headers.ETag?.ToString());
        Assert.Equal(updated, await ReadAsync(client, transfer.Id));

        Assert.Equal(960m, (await client.GetJsonAsync<AccountResponse>($"/api/accounts/{checking.Id}")).Balance);
        Assert.Equal(0m, (await client.GetJsonAsync<AccountResponse>($"/api/accounts/{savings.Id}")).Balance);
        Assert.Equal(40m, (await client.GetJsonAsync<AccountResponse>($"/api/accounts/{cash.Id}")).Balance);
        var incoming = await client.GetJsonAsync<TransactionResponse>($"/api/transactions/{transfer.IncomingTransactionId}");
        Assert.Equal((cash.Id, new DateOnly(2026, 11, 5), "Prelievo"), (incoming.AccountId, incoming.Date, incoming.Description));
    }

    [Fact]
    public async Task Update_OnAnAccountArchivedLater_IsAllowed_ButNotToANewArchivedAccount()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();
        var archived = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var client = ClientFor(userId);
        var transfer = await ReadAsync(client, (await Database.CreateTransferAsync(checking, savings, 100m, cancellationToken: Ct)).Id);
        await client.PostEmptyAsync($"/api/accounts/{savings.Id}/archive");
        await client.PostEmptyAsync($"/api/accounts/{archived.Id}/archive");

        var correction = await client.PutWithVersionAsync($"{Transfers}/{transfer.Id}", Body(checking, savings, amount: 120m), transfer.Version);
        var corrected = await correction.ReadAsync<TransferResponse>();
        var toArchived = await client.PutWithVersionAsync($"{Transfers}/{transfer.Id}", Body(checking, archived), corrected.Version);

        Assert.Equal(HttpStatusCode.OK, correction.StatusCode);
        Assert.Equal(120m, corrected.Amount);
        await Problems.AssertAsync(toArchived, HttpStatusCode.Conflict, DomainErrors.AccountArchived);
    }

    [Fact]
    public async Task Update_WithAMissingAmount_Returns400OnAmount()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();
        var client = ClientFor(userId);
        var transfer = await ReadAsync(client, (await Database.CreateTransferAsync(checking, savings, cancellationToken: Ct)).Id);
        var body = Body(checking, savings);
        body.Remove("amount");

        var response = await client.PutWithVersionAsync($"{Transfers}/{transfer.Id}", body, transfer.Version);

        Problems.AssertField(await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed), "amount");
    }

    [Fact]
    public async Task Update_ToAnotherUsersAccount_Returns400OnTheField()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();
        var (_, otherAccount, _) = await UserWithTwoAccountsAsync();
        var client = ClientFor(userId);
        var transfer = await ReadAsync(client, (await Database.CreateTransferAsync(checking, savings, cancellationToken: Ct)).Id);

        var response = await client.PutWithVersionAsync($"{Transfers}/{transfer.Id}", Body(checking, otherAccount), transfer.Version);

        Problems.AssertField(await Problems.AssertAsync(response, HttpStatusCode.BadRequest, "account.not_found"), "toAccountId");
    }

    [Fact]
    public async Task Update_WithoutIfMatch_Returns428()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();
        var transfer = await Database.CreateTransferAsync(checking, savings, cancellationToken: Ct);

        var response = await ClientFor(userId).PutAsJsonAsync($"{Transfers}/{transfer.Id}", Body(checking, savings), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionRequired, ApiErrors.VersionRequired);
    }

    [Fact]
    public async Task Update_WithAStaleVersion_Returns412_AndChangesNeitherLeg()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();
        var client = ClientFor(userId);
        var transfer = await ReadAsync(client, (await Database.CreateTransferAsync(checking, savings, 100m, cancellationToken: Ct)).Id);

        var first = await client.PutWithVersionAsync($"{Transfers}/{transfer.Id}", Body(checking, savings, amount: 150m), transfer.Version);
        var stale = await client.PutWithVersionAsync($"{Transfers}/{transfer.Id}", Body(checking, savings, amount: 999m), transfer.Version);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await Problems.AssertAsync(stale, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Equal(150m, (await ReadAsync(client, transfer.Id)).Amount);
        Assert.Equal(150m, (await client.GetJsonAsync<TransactionResponse>($"/api/transactions/{transfer.IncomingTransactionId}")).Amount);
    }

    [Fact]
    public async Task Update_AnotherUsersTransfer_Returns404()
    {
        var (otherUserId, otherChecking, otherSavings) = await UserWithTwoAccountsAsync();
        var other = await ReadAsync(ClientFor(otherUserId), (await Database.CreateTransferAsync(otherChecking, otherSavings, cancellationToken: Ct)).Id);
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();

        var response = await ClientFor(userId).PutWithVersionAsync($"{Transfers}/{other.Id}", Body(checking, savings), other.Version);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
    }

    [Fact]
    public async Task Update_WithoutSession_Returns401()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();
        var transfer = await ReadAsync(ClientFor(userId), (await Database.CreateTransferAsync(checking, savings, cancellationToken: Ct)).Id);

        var response = await AnonymousClient().PutWithVersionAsync($"{Transfers}/{transfer.Id}", Body(checking, savings), transfer.Version);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Delete ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_RemovesBothLegs_AndRestoresBothBalances()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync(checkingBalance: 500m);
        var client = ClientFor(userId);
        var transfer = await ReadAsync(client, (await Database.CreateTransferAsync(checking, savings, 200m, cancellationToken: Ct)).Id);

        var response = await client.DeleteWithVersionAsync($"{Transfers}/{transfer.Id}", transfer.Version);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Transfers}/{transfer.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/transactions/{transfer.OutgoingTransactionId}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/transactions/{transfer.IncomingTransactionId}", Ct)).StatusCode);
        Assert.Equal(500m, (await client.GetJsonAsync<AccountResponse>($"/api/accounts/{checking.Id}")).Balance);
        Assert.Equal(0m, (await client.GetJsonAsync<AccountResponse>($"/api/accounts/{savings.Id}")).Balance);
    }

    [Fact]
    public async Task Delete_WithoutIfMatch_Returns428()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();
        var transfer = await Database.CreateTransferAsync(checking, savings, cancellationToken: Ct);

        var response = await ClientFor(userId).DeleteAsync($"{Transfers}/{transfer.Id}", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionRequired, ApiErrors.VersionRequired);
    }

    [Fact]
    public async Task Delete_WithAStaleVersion_Returns412_AndKeepsIt()
    {
        var (userId, checking, savings) = await UserWithTwoAccountsAsync();
        var client = ClientFor(userId);
        var transfer = await ReadAsync(client, (await Database.CreateTransferAsync(checking, savings, cancellationToken: Ct)).Id);

        var response = await client.DeleteWithVersionAsync($"{Transfers}/{transfer.Id}", transfer.Version + 1);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Transfers}/{transfer.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_AnotherUsersTransfer_Returns404_AndKeepsIt()
    {
        var (otherUserId, otherChecking, otherSavings) = await UserWithTwoAccountsAsync();
        var other = await ReadAsync(ClientFor(otherUserId), (await Database.CreateTransferAsync(otherChecking, otherSavings, cancellationToken: Ct)).Id);

        var response = await ClientFor(await NewUserAsync()).DeleteWithVersionAsync($"{Transfers}/{other.Id}", other.Version);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
        Assert.Equal(HttpStatusCode.OK, (await ClientFor(otherUserId).GetAsync($"{Transfers}/{other.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutSession_Returns401()
    {
        var (_, checking, savings) = await UserWithTwoAccountsAsync();
        var transfer = await Database.CreateTransferAsync(checking, savings, cancellationToken: Ct);

        var response = await AnonymousClient().DeleteWithVersionAsync($"{Transfers}/{transfer.Id}", 1);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Helpers -----------------------------------------------------------------------------------------------

    private async Task<(Guid UserId, Account Checking, Account Savings)> UserWithTwoAccountsAsync(decimal checkingBalance = 0m)
    {
        var userId = await NewUserAsync();
        var checking = await Database.CreateAccountAsync(userId, initialBalance: checkingBalance, cancellationToken: Ct);
        var savings = await Database.CreateAccountAsync(userId, type: AccountType.Savings, cancellationToken: Ct);
        return (userId, checking, savings);
    }

    /// <summary>A valid create or update body; tests remove or replace one field.</summary>
    private static Dictionary<string, object?> Body(Account from, Account to, decimal amount = 50m) => new()
    {
        ["fromAccountId"] = from.Id,
        ["toAccountId"] = to.Id,
        ["date"] = "2026-11-12",
        ["amount"] = amount,
    };

    private static Task<TransferResponse> ReadAsync(HttpClient client, Guid id) =>
        client.GetJsonAsync<TransferResponse>($"{Transfers}/{id}");
}
