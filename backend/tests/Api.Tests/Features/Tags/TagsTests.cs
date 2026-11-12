using System.Net;
using System.Net.Http.Json;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Responses;
using PersonalFinance.Api.Features.Tags;
using PersonalFinance.Api.Features.Transactions;
using PersonalFinance.Api.Tests.Support;
using PersonalFinance.Testing;

namespace PersonalFinance.Api.Tests.Features.Tags;

// Tags, part of the transactions module (docs/backend/api-endpoints.md#tags): list, rename, delete with success, 401,
// validation, isolation and versions. Creation goes through transactions (TransactionsTests).
public sealed class TagsTests(ApiServer server) : ApiTest(server)
{
    private const string Tags = "/api/tags";

    // ---- List --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task List_ReturnsTheTagsOfTheUserByName_WithHowOftenTheyAreUsed()
    {
        var userId = await NewUserAsync();
        var account = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var work = await Database.CreateTagAsync(userId, "lavoro", Ct);
        var holiday = await Database.CreateTagAsync(userId, "Vacanza", Ct);
        await Database.CreateExpenseAsync(account, tags: [holiday], cancellationToken: Ct);
        await Database.CreateExpenseAsync(account, tags: [holiday, work], cancellationToken: Ct);
        await Database.CreateTagAsync(await NewUserAsync(), "altro utente", Ct);

        var response = await ClientFor(userId).GetAsync(Tags, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.ReadAsync<ListResponse<TagResponse>>()).Items;
        Assert.Equal(new[] { ("lavoro", 1), ("Vacanza", 2) }, items.Select(tag => (tag.Name, tag.TransactionCount)));
        Assert.All(items, tag => Assert.NotEqual(0u, tag.Version));
    }

    [Fact]
    public async Task List_WithoutSession_Returns401()
    {
        var response = await AnonymousClient().GetAsync(Tags, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Rename ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Rename_ChangesTheNameOnEveryTransaction()
    {
        var userId = await NewUserAsync();
        var account = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var typo = await Database.CreateTagAsync(userId, "vacnza", Ct);
        var expense = await Database.CreateExpenseAsync(account, tags: [typo], cancellationToken: Ct);
        var client = ClientFor(userId);
        var tag = await ReadAsync(client, typo.Id);

        var response = await client.PutWithVersionAsync($"{Tags}/{tag.Id}", new { name = " vacanza " }, tag.Version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renamed = await response.ReadAsync<TagResponse>();
        Assert.Equal(("vacanza", 1), (renamed.Name, renamed.TransactionCount));
        Assert.NotEqual(tag.Version, renamed.Version);
        Assert.Equal(EntityTag.Format(renamed.Version), response.Headers.ETag?.ToString());
        Assert.Equal(new[] { "vacanza" }, (await client.GetJsonAsync<TransactionResponse>($"/api/transactions/{expense.Id}")).Tags);
    }

    [Fact]
    public async Task Rename_ToTheNameOfAnotherTag_Returns409()
    {
        var userId = await NewUserAsync();
        await Database.CreateTagAsync(userId, "lavoro", Ct);
        var client = ClientFor(userId);
        var tag = await ReadAsync(client, (await Database.CreateTagAsync(userId, "ufficio", Ct)).Id);

        var response = await client.PutWithVersionAsync($"{Tags}/{tag.Id}", new { name = "LAVORO" }, tag.Version);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, ApiErrors.TagNameTaken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a-tag-name-that-is-longer-than-fifty-characters-in-total")]
    public async Task Rename_WithAnInvalidName_Returns400OnName(string name)
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var tag = await ReadAsync(client, (await Database.CreateTagAsync(userId, cancellationToken: Ct)).Id);

        var response = await client.PutWithVersionAsync($"{Tags}/{tag.Id}", new { name }, tag.Version);

        Problems.AssertField(await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed), "name");
    }

    [Fact]
    public async Task Rename_WithoutIfMatch_Returns428()
    {
        var userId = await NewUserAsync();
        var tag = await Database.CreateTagAsync(userId, cancellationToken: Ct);

        var response = await ClientFor(userId).PutAsJsonAsync($"{Tags}/{tag.Id}", new { name = "nuovo" }, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionRequired, ApiErrors.VersionRequired);
    }

    [Fact]
    public async Task Rename_WithAStaleVersion_Returns412_AndChangesNothing()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var tag = await ReadAsync(client, (await Database.CreateTagAsync(userId, "prima", Ct)).Id);

        var response = await client.PutWithVersionAsync($"{Tags}/{tag.Id}", new { name = "dopo" }, tag.Version + 1);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Equal("prima", (await ReadAsync(client, tag.Id)).Name);
    }

    [Fact]
    public async Task Rename_AnotherUsersTag_Returns404()
    {
        var otherUserId = await NewUserAsync();
        var other = await ReadAsync(ClientFor(otherUserId), (await Database.CreateTagAsync(otherUserId, "suo", Ct)).Id);

        var response = await ClientFor(await NewUserAsync()).PutWithVersionAsync($"{Tags}/{other.Id}", new { name = "mio" }, other.Version);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
        Assert.Equal("suo", (await ReadAsync(ClientFor(otherUserId), other.Id)).Name);
    }

    [Fact]
    public async Task Rename_WithoutSession_Returns401()
    {
        var tag = await Database.CreateTagAsync(await NewUserAsync(), cancellationToken: Ct);

        var response = await AnonymousClient().PutWithVersionAsync($"{Tags}/{tag.Id}", new { name = "nuovo" }, 1);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Delete ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_RemovesTheTagFromItsTransactions_AndKeepsThem()
    {
        var userId = await NewUserAsync();
        var account = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var holiday = await Database.CreateTagAsync(userId, "vacanza", Ct);
        var work = await Database.CreateTagAsync(userId, "lavoro", Ct);
        var expense = await Database.CreateExpenseAsync(account, tags: [holiday, work], cancellationToken: Ct);
        var client = ClientFor(userId);
        var tag = await ReadAsync(client, holiday.Id);

        var response = await client.DeleteWithVersionAsync($"{Tags}/{tag.Id}", tag.Version);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new[] { "lavoro" }, (await client.GetJsonAsync<TransactionResponse>($"/api/transactions/{expense.Id}")).Tags);
        Assert.DoesNotContain((await client.GetJsonAsync<ListResponse<TagResponse>>(Tags)).Items, item => item.Id == holiday.Id);
    }

    [Fact]
    public async Task Delete_WithoutIfMatch_Returns428()
    {
        var userId = await NewUserAsync();
        var tag = await Database.CreateTagAsync(userId, cancellationToken: Ct);

        var response = await ClientFor(userId).DeleteAsync($"{Tags}/{tag.Id}", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionRequired, ApiErrors.VersionRequired);
    }

    [Fact]
    public async Task Delete_WithAStaleVersion_Returns412_AndKeepsIt()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var tag = await ReadAsync(client, (await Database.CreateTagAsync(userId, cancellationToken: Ct)).Id);

        var response = await client.DeleteWithVersionAsync($"{Tags}/{tag.Id}", tag.Version + 1);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Contains((await client.GetJsonAsync<ListResponse<TagResponse>>(Tags)).Items, item => item.Id == tag.Id);
    }

    [Fact]
    public async Task Delete_AnotherUsersTag_Returns404_AndKeepsIt()
    {
        var otherUserId = await NewUserAsync();
        var other = await ReadAsync(ClientFor(otherUserId), (await Database.CreateTagAsync(otherUserId, cancellationToken: Ct)).Id);

        var response = await ClientFor(await NewUserAsync()).DeleteWithVersionAsync($"{Tags}/{other.Id}", other.Version);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
        Assert.Contains((await ClientFor(otherUserId).GetJsonAsync<ListResponse<TagResponse>>(Tags)).Items, item => item.Id == other.Id);
    }

    [Fact]
    public async Task Delete_WithoutSession_Returns401()
    {
        var tag = await Database.CreateTagAsync(await NewUserAsync(), cancellationToken: Ct);

        var response = await AnonymousClient().DeleteWithVersionAsync($"{Tags}/{tag.Id}", 1);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Helpers -----------------------------------------------------------------------------------------------

    /// <summary>There is no single-tag endpoint: the list is what the app reads.</summary>
    private static async Task<TagResponse> ReadAsync(HttpClient client, Guid id) =>
        (await client.GetJsonAsync<ListResponse<TagResponse>>(Tags)).Items.Single(tag => tag.Id == id);
}
