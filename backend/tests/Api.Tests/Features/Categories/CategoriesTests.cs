using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Responses;
using PersonalFinance.Api.Features.Categories;
using PersonalFinance.Api.Tests.Support;
using PersonalFinance.Domain.Categories;
using PersonalFinance.Domain.Common;
using PersonalFinance.Testing;

namespace PersonalFinance.Api.Tests.Features.Categories;

// Module categories (docs/backend/api-endpoints.md#categories): every endpoint with success, 401, validation,
// isolation and, for writes, versions (docs/backend/testing.md#minimum-per-endpoint).
public sealed class CategoriesTests(ApiServer server) : ApiTest(server)
{
    private const string Categories = "/api/categories";

    // ---- List --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task List_ExpensesFirst_EachParentFollowedByItsSubcategories_ByName()
    {
        var userId = await NewUserAsync();
        var salary = await Database.CreateCategoryAsync(userId, name: "Stipendio", kind: CategoryKind.Income, cancellationToken: Ct);
        var transport = await Database.CreateCategoryAsync(userId, name: "trasporti", cancellationToken: Ct);
        var train = await Database.CreateCategoryAsync(userId, name: "Treno", parent: transport, cancellationToken: Ct);
        var car = await Database.CreateCategoryAsync(userId, name: "auto", parent: transport, cancellationToken: Ct);
        var home = await Database.CreateCategoryAsync(userId, name: "Casa", cancellationToken: Ct);

        var response = await ClientFor(userId).GetAsync(Categories, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.ReadAsync<ListResponse<CategoryResponse>>()).Items;
        Assert.Equal(new[] { home.Id, transport.Id, car.Id, train.Id, salary.Id }, items.Select(category => category.Id));
        Assert.Equal(transport.Id, items[2].ParentId);
        Assert.True(items[1].HasSubcategories);
        Assert.All(items, category => Assert.NotEqual(0u, category.Version));
    }

    [Fact]
    public async Task List_FiltersByKind()
    {
        var userId = await NewUserAsync();
        var salary = await Database.CreateCategoryAsync(userId, kind: CategoryKind.Income, cancellationToken: Ct);
        await Database.CreateCategoryAsync(userId, kind: CategoryKind.Expense, cancellationToken: Ct);

        var list = await ClientFor(userId).GetJsonAsync<ListResponse<CategoryResponse>>($"{Categories}?kind=income");

        Assert.Equal(new[] { salary.Id }, list.Items.Select(category => category.Id));
    }

    [Fact]
    public async Task List_HidesArchivedCategories_UnlessAsked()
    {
        var userId = await NewUserAsync();
        var active = await Database.CreateCategoryAsync(userId, name: "Attiva", cancellationToken: Ct);
        var archived = await Database.CreateCategoryAsync(userId, name: "Archiviata", cancellationToken: Ct);
        var archivedChild = await Database.CreateCategoryAsync(userId, name: "Figlia", parent: archived, cancellationToken: Ct);
        await ArchiveAsync(userId, archived.Id);
        var client = ClientFor(userId);

        var activeOnly = await client.GetJsonAsync<ListResponse<CategoryResponse>>(Categories);
        var all = await client.GetJsonAsync<ListResponse<CategoryResponse>>($"{Categories}?includeArchived=true");

        Assert.Equal(new[] { active.Id }, activeOnly.Items.Select(category => category.Id));
        Assert.Equal(new[] { archived.Id, archivedChild.Id, active.Id }, all.Items.Select(category => category.Id));
        Assert.True(all.Items[1].IsArchived);
    }

    [Theory]
    [InlineData("transfer")]
    [InlineData("1")]
    public async Task List_WithAnUnknownKind_Returns400OnKind(string kind)
    {
        var userId = await NewUserAsync();

        var response = await ClientFor(userId).GetAsync($"{Categories}?kind={kind}", Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.RequestInvalid);
        Problems.AssertField(problem, "kind");
    }

    [Fact]
    public async Task List_WithoutSession_Returns401()
    {
        var response = await AnonymousClient().GetAsync(Categories, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Fact]
    public async Task List_ContainsOnlyTheCategoriesOfTheUser()
    {
        var userId = await NewUserAsync();
        var mine = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct);

        var list = await ClientFor(userId).GetJsonAsync<ListResponse<CategoryResponse>>($"{Categories}?includeArchived=true");

        Assert.Equal(new[] { mine.Id }, list.Items.Select(category => category.Id));
    }

    // ---- Get ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Get_ReturnsTheCategoryWithItsVersionAsETag()
    {
        var userId = await NewUserAsync();
        var transport = await Database.CreateCategoryAsync(userId, name: "Trasporti", cancellationToken: Ct);
        var fuel = await Database.CreateCategoryAsync(userId, name: "Carburante", parent: transport, cancellationToken: Ct);

        var response = await ClientFor(userId).GetAsync($"{Categories}/{fuel.Id}", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadAsync<CategoryResponse>();
        Assert.Equal(new CategoryResponse(fuel.Id, "Carburante", CategoryKind.Expense, transport.Id, false, false, false, body.Version), body);
        Assert.Equal(EntityTag.Format(body.Version), response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task Get_IsInUse_WithTransactionsOrBudgets()
    {
        var userId = await NewUserAsync();
        var account = await Database.CreateAccountAsync(userId, cancellationToken: Ct);
        var withExpense = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var withBudget = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var unused = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateExpenseAsync(account, category: withExpense, cancellationToken: Ct);
        await Database.CreateBudgetAsync(withBudget, cancellationToken: Ct);
        var client = ClientFor(userId);

        Assert.True((await ReadAsync(client, withExpense.Id)).IsInUse);
        Assert.True((await ReadAsync(client, withBudget.Id)).IsInUse);
        Assert.False((await ReadAsync(client, unused.Id)).IsInUse);
    }

    [Fact]
    public async Task Get_WithoutSession_Returns401()
    {
        var category = await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct);

        var response = await AnonymousClient().GetAsync($"{Categories}/{category.Id}", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Fact]
    public async Task Get_AnotherUsersCategory_Returns404_LikeANonexistentOne()
    {
        var other = await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct);
        var client = ClientFor(await NewUserAsync());

        await Problems.AssertAsync(await client.GetAsync($"{Categories}/{other.Id}", Ct), HttpStatusCode.NotFound, ApiErrors.NotFound);
        await Problems.AssertAsync(await client.GetAsync($"{Categories}/{Guid.CreateVersion7()}", Ct), HttpStatusCode.NotFound, ApiErrors.NotFound);
    }

    // ---- Create ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_TopLevel_Returns201WithLocationAndETag()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);

        var response = await client.PostAsJsonAsync(Categories, new { name = " Viaggi ", kind = "expense" }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<CategoryResponse>();
        Assert.Equal(new CategoryResponse(created.Id, "Viaggi", CategoryKind.Expense, null, false, false, false, created.Version), created);
        Assert.Equal(EntityTag.Format(created.Version), response.Headers.ETag?.ToString());
        Assert.Equal($"{Categories}/{created.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(created, await ReadAsync(client, created.Id));
    }

    [Fact]
    public async Task Create_Subcategory_Returns201UnderTheParent()
    {
        var userId = await NewUserAsync();
        var salary = await Database.CreateCategoryAsync(userId, kind: CategoryKind.Income, cancellationToken: Ct);

        var response = await ClientFor(userId).PostAsJsonAsync(Categories, new { name = "Tredicesima", kind = "income", parentId = salary.Id }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<CategoryResponse>();
        Assert.Equal(salary.Id, created.ParentId);
        Assert.Equal(CategoryKind.Income, created.Kind);
    }

    [Fact]
    public async Task Create_WithoutSession_Returns401()
    {
        var response = await AnonymousClient().PostAsJsonAsync(Categories, ValidBody(), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("kind")]
    public async Task Create_WithoutARequiredField_Returns400OnThatField(string field)
    {
        var userId = await NewUserAsync();
        var body = ValidBody();
        body.Remove(field);

        var response = await ClientFor(userId).PostAsJsonAsync(Categories, body, Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed);
        Problems.AssertField(problem, field);
    }

    [Fact]
    public async Task Create_WithANameTooLong_Returns400OnName()
    {
        var userId = await NewUserAsync();
        var body = ValidBody();
        body["name"] = new string('a', Category.NameMaxLength + 1);

        var response = await ClientFor(userId).PostAsJsonAsync(Categories, body, Ct);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed);
        Problems.AssertField(problem, "name");
    }

    [Fact]
    public async Task Create_WithAnUnknownKind_Returns400RequestInvalid()
    {
        var userId = await NewUserAsync();
        var body = ValidBody();
        body["kind"] = "transfer";

        var response = await ClientFor(userId).PostAsJsonAsync(Categories, body, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.RequestInvalid);
    }

    [Fact]
    public async Task Create_UnderAnotherUsersCategory_Returns400OnParentId_LikeANonexistentOne()
    {
        var userId = await NewUserAsync();
        var other = await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct);
        var client = ClientFor(userId);

        foreach (var parentId in new[] { other.Id, Guid.CreateVersion7() })
        {
            var body = ValidBody();
            body["parentId"] = parentId;

            var response = await client.PostAsJsonAsync(Categories, body, Ct);

            var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, "category.not_found");
            Problems.AssertField(problem, "parentId");
        }
    }

    [Fact]
    public async Task Create_UnderASubcategory_Returns400TooDeep()
    {
        var userId = await NewUserAsync();
        var transport = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var car = await Database.CreateCategoryAsync(userId, parent: transport, cancellationToken: Ct);
        var body = ValidBody();
        body["parentId"] = car.Id;

        var response = await ClientFor(userId).PostAsJsonAsync(Categories, body, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, DomainErrors.CategoryTooDeep);
    }

    [Fact]
    public async Task Create_WithAKindDifferentFromTheParent_Returns400KindMismatch()
    {
        var userId = await NewUserAsync();
        var salary = await Database.CreateCategoryAsync(userId, kind: CategoryKind.Income, cancellationToken: Ct);

        var response = await ClientFor(userId).PostAsJsonAsync(Categories, new { name = "Bonus", kind = "expense", parentId = salary.Id }, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, DomainErrors.CategoryKindMismatch);
    }

    [Fact]
    public async Task Create_UnderAnArchivedParent_Returns409()
    {
        var userId = await NewUserAsync();
        var archived = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await ArchiveAsync(userId, archived.Id);
        var body = ValidBody();
        body["parentId"] = archived.Id;

        var response = await ClientFor(userId).PostAsJsonAsync(Categories, body, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, DomainErrors.CategoryArchived);
    }

    [Fact]
    public async Task Create_WithANameUsedAtTheSamePlace_Returns409_EvenIfArchivedOrInAnotherCase()
    {
        var userId = await NewUserAsync();
        var existing = await Database.CreateCategoryAsync(userId, name: "Svago", cancellationToken: Ct);
        await ArchiveAsync(userId, existing.Id);

        var response = await ClientFor(userId).PostAsJsonAsync(Categories, new { name = "SVAGO", kind = "expense" }, Ct);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, ApiErrors.CategoryNameTaken);
    }

    [Fact]
    public async Task Create_WithANameUsedElsewhere_Succeeds()
    {
        var userId = await NewUserAsync();
        var transport = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateCategoryAsync(userId, name: "Altro", cancellationToken: Ct);
        await Database.CreateCategoryAsync(await NewUserAsync(), name: "Regali", kind: CategoryKind.Income, cancellationToken: Ct);
        var client = ClientFor(userId);

        var otherKind = await client.PostAsJsonAsync(Categories, new { name = "Altro", kind = "income" }, Ct);
        var otherParent = await client.PostAsJsonAsync(Categories, new { name = "Altro", kind = "expense", parentId = transport.Id }, Ct);
        var otherUser = await client.PostAsJsonAsync(Categories, new { name = "Regali", kind = "income" }, Ct);

        Assert.Equal(HttpStatusCode.Created, otherKind.StatusCode);
        Assert.Equal(HttpStatusCode.Created, otherParent.StatusCode);
        Assert.Equal(HttpStatusCode.Created, otherUser.StatusCode);
    }

    // ---- Update ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Update_RenamesAndMoves_AndReturnsTheNewVersion()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var transport = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var category = await ReadAsync(client, (await Database.CreateCategoryAsync(userId, name: "Benzina", cancellationToken: Ct)).Id);

        var response = await client.PutWithVersionAsync(
            $"{Categories}/{category.Id}", new { name = "Carburante", kind = "expense", parentId = transport.Id }, category.Version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<CategoryResponse>();
        Assert.Equal(new CategoryResponse(category.Id, "Carburante", CategoryKind.Expense, transport.Id, false, false, false, updated.Version), updated);
        Assert.NotEqual(category.Version, updated.Version);
        Assert.Equal(EntityTag.Format(updated.Version), response.Headers.ETag?.ToString());
        Assert.Equal(updated, await ReadAsync(client, category.Id));
    }

    [Fact]
    public async Task Update_ChangesTheKindOfAnUnusedCategory()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var category = await ReadAsync(client, (await Database.CreateCategoryAsync(userId, cancellationToken: Ct)).Id);

        var response = await client.PutWithVersionAsync($"{Categories}/{category.Id}", new { name = category.Name, kind = "income" }, category.Version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(CategoryKind.Income, (await response.ReadAsync<CategoryResponse>()).Kind);
    }

    [Fact]
    public async Task Update_KindOfACategoryInUse_Returns409_ButTheNameCanChange()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var created = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateExpenseAsync(await Database.CreateAccountAsync(userId, cancellationToken: Ct), category: created, cancellationToken: Ct);
        var category = await ReadAsync(client, created.Id);

        var newKind = await client.PutWithVersionAsync($"{Categories}/{category.Id}", new { name = category.Name, kind = "income" }, category.Version);
        var newName = await client.PutWithVersionAsync($"{Categories}/{category.Id}", new { name = "Rinominata", kind = "expense" }, category.Version);

        await Problems.AssertAsync(newKind, HttpStatusCode.Conflict, DomainErrors.CategoryKindLocked);
        Assert.Equal(HttpStatusCode.OK, newName.StatusCode);
    }

    [Fact]
    public async Task Update_MovingACategoryWithSubcategories_Returns409()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var home = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var transport = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateCategoryAsync(userId, parent: transport, cancellationToken: Ct);
        var category = await ReadAsync(client, transport.Id);

        var response = await client.PutWithVersionAsync(
            $"{Categories}/{category.Id}", new { name = category.Name, kind = "expense", parentId = home.Id }, category.Version);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, DomainErrors.CategoryHasSubcategories);
    }

    [Fact]
    public async Task Update_MovingABudgetedCategoryUnderABudgetedParent_Returns409()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var home = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var furniture = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateBudgetAsync(home, cancellationToken: Ct);
        await Database.CreateBudgetAsync(furniture, cancellationToken: Ct);
        var category = await ReadAsync(client, furniture.Id);

        var response = await client.PutWithVersionAsync(
            $"{Categories}/{category.Id}", new { name = category.Name, kind = "expense", parentId = home.Id }, category.Version);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, DomainErrors.BudgetBranchConflict);
    }

    [Fact]
    public async Task Update_UnderItself_Returns400()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var category = await ReadAsync(client, (await Database.CreateCategoryAsync(userId, cancellationToken: Ct)).Id);

        var response = await client.PutWithVersionAsync(
            $"{Categories}/{category.Id}", new { name = category.Name, kind = "expense", parentId = category.Id }, category.Version);

        await Problems.AssertAsync(response, HttpStatusCode.BadRequest, DomainErrors.CategoryParentItself);
    }

    [Fact]
    public async Task Update_UnderAnotherUsersCategory_Returns400OnParentId()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var other = await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct);
        var category = await ReadAsync(client, (await Database.CreateCategoryAsync(userId, cancellationToken: Ct)).Id);

        var response = await client.PutWithVersionAsync(
            $"{Categories}/{category.Id}", new { name = category.Name, kind = "expense", parentId = other.Id }, category.Version);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, "category.not_found");
        Problems.AssertField(problem, "parentId");
    }

    [Fact]
    public async Task Update_WithTheNameOfASibling_Returns409()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        await Database.CreateCategoryAsync(userId, name: "Svago", cancellationToken: Ct);
        var category = await ReadAsync(client, (await Database.CreateCategoryAsync(userId, cancellationToken: Ct)).Id);

        var response = await client.PutWithVersionAsync($"{Categories}/{category.Id}", new { name = "svago", kind = "expense" }, category.Version);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, ApiErrors.CategoryNameTaken);
    }

    [Fact]
    public async Task Update_WithoutSession_Returns401()
    {
        var category = await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct);

        var response = await AnonymousClient().PutWithVersionAsync($"{Categories}/{category.Id}", ValidBody(), 1);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Fact]
    public async Task Update_WithAnEmptyName_Returns400OnName()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var category = await ReadAsync(client, (await Database.CreateCategoryAsync(userId, cancellationToken: Ct)).Id);

        var response = await client.PutWithVersionAsync($"{Categories}/{category.Id}", new { name = "", kind = "expense" }, category.Version);

        var problem = await Problems.AssertAsync(response, HttpStatusCode.BadRequest, ApiErrors.ValidationFailed);
        Problems.AssertField(problem, "name");
    }

    [Fact]
    public async Task Update_WithoutIfMatch_Returns428()
    {
        var userId = await NewUserAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);

        var response = await ClientFor(userId).PutAsJsonAsync($"{Categories}/{category.Id}", ValidBody(), Ct);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionRequired, ApiErrors.VersionRequired);
    }

    [Fact]
    public async Task Update_WithAStaleVersion_Returns412_AndChangesNothing()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var category = await ReadAsync(client, (await Database.CreateCategoryAsync(userId, name: "Prima", cancellationToken: Ct)).Id);

        var response = await client.PutWithVersionAsync($"{Categories}/{category.Id}", new { name = "Dopo", kind = "expense" }, category.Version + 1);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Equal("Prima", (await ReadAsync(client, category.Id)).Name);
    }

    [Fact]
    public async Task Update_AnotherUsersCategory_Returns404()
    {
        var other = await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct);

        var response = await ClientFor(await NewUserAsync()).PutWithVersionAsync($"{Categories}/{other.Id}", ValidBody(), 1);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
    }

    // ---- Archive and unarchive ---------------------------------------------------------------------------------

    [Fact]
    public async Task Archive_ArchivesTheSubcategoriesToo_AndChangesTheVersion()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var transport = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var fuel = await Database.CreateCategoryAsync(userId, parent: transport, cancellationToken: Ct);
        var before = await ReadAsync(client, transport.Id);

        var response = await client.PostEmptyAsync($"{Categories}/{transport.Id}/archive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var archived = await response.ReadAsync<CategoryResponse>();
        Assert.True(archived.IsArchived);
        Assert.NotEqual(before.Version, archived.Version);
        Assert.Equal(EntityTag.Format(archived.Version), response.Headers.ETag?.ToString());
        Assert.True((await ReadAsync(client, fuel.Id)).IsArchived);
        Assert.Empty((await client.GetJsonAsync<ListResponse<CategoryResponse>>(Categories)).Items);
    }

    [Fact]
    public async Task Archive_IsIdempotent()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var first = await (await client.PostEmptyAsync($"{Categories}/{category.Id}/archive")).ReadAsync<CategoryResponse>();

        var response = await client.PostEmptyAsync($"{Categories}/{category.Id}/archive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(first, await response.ReadAsync<CategoryResponse>());
    }

    [Fact]
    public async Task Unarchive_RestoresOnlyTheParent()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var transport = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var fuel = await Database.CreateCategoryAsync(userId, parent: transport, cancellationToken: Ct);
        await ArchiveAsync(userId, transport.Id);

        var response = await client.PostEmptyAsync($"{Categories}/{transport.Id}/unarchive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.ReadAsync<CategoryResponse>()).IsArchived);
        Assert.True((await ReadAsync(client, fuel.Id)).IsArchived);
    }

    [Fact]
    public async Task Unarchive_ASubcategoryOfAnArchivedParent_Returns409()
    {
        var userId = await NewUserAsync();
        var transport = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        var fuel = await Database.CreateCategoryAsync(userId, parent: transport, cancellationToken: Ct);
        await ArchiveAsync(userId, transport.Id);

        var response = await ClientFor(userId).PostEmptyAsync($"{Categories}/{fuel.Id}/unarchive");

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, DomainErrors.CategoryArchived);
    }

    [Theory]
    [InlineData("archive")]
    [InlineData("unarchive")]
    public async Task ArchiveActions_WithoutSession_Return401(string action)
    {
        var category = await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct);

        var response = await AnonymousClient().PostEmptyAsync($"{Categories}/{category.Id}/{action}");

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Theory]
    [InlineData("archive")]
    [InlineData("unarchive")]
    public async Task ArchiveActions_OnAnotherUsersCategory_Return404_AndChangeNothing(string action)
    {
        var otherUserId = await NewUserAsync();
        var other = await Database.CreateCategoryAsync(otherUserId, cancellationToken: Ct);

        var response = await ClientFor(await NewUserAsync()).PostEmptyAsync($"{Categories}/{other.Id}/{action}");

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
        Assert.False((await ReadAsync(ClientFor(otherUserId), other.Id)).IsArchived);
    }

    // ---- Delete ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_AnUnusedCategory_Returns204_AndRemovesIt()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var category = await ReadAsync(client, (await Database.CreateCategoryAsync(userId, cancellationToken: Ct)).Id);

        var response = await client.DeleteWithVersionAsync($"{Categories}/{category.Id}", category.Version);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Categories}/{category.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_ACategoryUsedByATransaction_Returns409()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var created = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateExpenseAsync(await Database.CreateAccountAsync(userId, cancellationToken: Ct), category: created, cancellationToken: Ct);
        var category = await ReadAsync(client, created.Id);

        var response = await client.DeleteWithVersionAsync($"{Categories}/{category.Id}", category.Version);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, DomainErrors.CategoryInUse);
    }

    [Fact]
    public async Task Delete_ACategoryWithABudget_Returns409()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var created = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateBudgetAsync(created, cancellationToken: Ct);
        var category = await ReadAsync(client, created.Id);

        var response = await client.DeleteWithVersionAsync($"{Categories}/{category.Id}", category.Version);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, DomainErrors.CategoryInUse);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Categories}/{category.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_ACategoryWithSubcategories_Returns409()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var transport = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);
        await Database.CreateCategoryAsync(userId, parent: transport, cancellationToken: Ct);
        var category = await ReadAsync(client, transport.Id);

        var response = await client.DeleteWithVersionAsync($"{Categories}/{category.Id}", category.Version);

        await Problems.AssertAsync(response, HttpStatusCode.Conflict, DomainErrors.CategoryHasSubcategories);
    }

    [Fact]
    public async Task Delete_WithoutSession_Returns401()
    {
        var category = await Database.CreateCategoryAsync(await NewUserAsync(), cancellationToken: Ct);

        var response = await AnonymousClient().DeleteWithVersionAsync($"{Categories}/{category.Id}", 1);

        await Problems.AssertAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    [Fact]
    public async Task Delete_WithoutIfMatch_Returns428()
    {
        var userId = await NewUserAsync();
        var category = await Database.CreateCategoryAsync(userId, cancellationToken: Ct);

        var response = await ClientFor(userId).DeleteAsync($"{Categories}/{category.Id}", Ct);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionRequired, ApiErrors.VersionRequired);
    }

    [Fact]
    public async Task Delete_WithAStaleVersion_Returns412_AndKeepsTheCategory()
    {
        var userId = await NewUserAsync();
        var client = ClientFor(userId);
        var category = await ReadAsync(client, (await Database.CreateCategoryAsync(userId, cancellationToken: Ct)).Id);

        var response = await client.DeleteWithVersionAsync($"{Categories}/{category.Id}", category.Version + 1);

        await Problems.AssertAsync(response, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Categories}/{category.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_AnotherUsersCategory_Returns404_AndKeepsIt()
    {
        var otherUserId = await NewUserAsync();
        var other = await ReadAsync(ClientFor(otherUserId), (await Database.CreateCategoryAsync(otherUserId, cancellationToken: Ct)).Id);

        var response = await ClientFor(await NewUserAsync()).DeleteWithVersionAsync($"{Categories}/{other.Id}", other.Version);

        await Problems.AssertAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
        Assert.Equal(HttpStatusCode.OK, (await ClientFor(otherUserId).GetAsync($"{Categories}/{other.Id}", Ct)).StatusCode);
    }

    // ---- Helpers -----------------------------------------------------------------------------------------------

    /// <summary>A valid create or update body for a top-level expense category; tests remove or replace one field.</summary>
    private static Dictionary<string, object?> ValidBody() => new()
    {
        ["name"] = TestData.UniqueName("Categoria"),
        ["kind"] = "expense",
    };

    private static Task<CategoryResponse> ReadAsync(HttpClient client, Guid id) =>
        client.GetJsonAsync<CategoryResponse>($"{Categories}/{id}");

    /// <summary>Archives a category and its subcategories through the domain, as the archive endpoint does.</summary>
    private async Task ArchiveAsync(Guid userId, Guid categoryId)
    {
        await using var db = Database.CreateContext(userId);
        var category = await db.Categories.SingleAsync(item => item.Id == categoryId, Ct);
        var subcategories = await db.Categories.Where(item => item.ParentId == categoryId).ToListAsync(Ct);
        category.Archive(subcategories);
        await db.SaveChangesAsync(Ct);
    }
}
