using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Endpoints;
using PersonalFinance.Api.Common.Errors;
using PersonalFinance.Api.Common.Security;
using PersonalFinance.Api.Tests.Support;
using PersonalFinance.Domain.Accounts;
using PersonalFinance.Domain.Common;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Testing;

namespace PersonalFinance.Api.Tests.Common;

// Error responses, versions and isolation on a host with sample endpoints over the class database
// (docs/backend/testing.md#mechanism-tests). Resource endpoints rely on exactly these mechanisms from block 4 on.
public sealed class ErrorHandlingTests(ApiServer server) : ApiTest(server)
{
    // ---- Domain errors -----------------------------------------------------------------------------------------

    [Fact]
    public async Task InvalidValue_Returns400WithTheDomainCode()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().GetAsync($"/api/samples/domain/{DomainErrors.AmountNotPositive}", Ct);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, DomainErrors.AmountNotPositive);
        Assert.Equal(DomainMessage, problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task StateConflict_Returns409WithTheDomainCode()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().GetAsync($"/api/samples/domain/{DomainErrors.AccountArchived}", Ct);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, DomainErrors.AccountArchived);
    }

    [Fact]
    public async Task ProgrammingError_Returns500WithoutDetails()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().GetAsync($"/api/samples/domain/{DomainErrors.DifferentUser}", Ct);

        var problem = await AssertProblemAsync(response, HttpStatusCode.InternalServerError, ApiErrors.ServerError);
        Assert.DoesNotContain(DomainMessage, problem.GetRawText(), StringComparison.Ordinal);
    }

    // ---- Other errors ------------------------------------------------------------------------------------------

    [Fact]
    public async Task UnexpectedException_Returns500WithoutDetails()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().GetAsync("/api/samples/crash", Ct);

        var problem = await AssertProblemAsync(response, HttpStatusCode.InternalServerError, ApiErrors.ServerError);
        Assert.DoesNotContain(SecretMessage, problem.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyNotFound_GetsAProblemBody()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().GetAsync("/api/samples/missing", Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
    }

    [Fact]
    public async Task NoSession_Returns401WithAProblemBody()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().GetAsync("/api/samples/private", Ct);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, ApiErrors.AuthenticationRequired);
    }

    // ---- Duplicates --------------------------------------------------------------------------------------------

    [Fact]
    public async Task DuplicateName_Returns409WithTheNameTakenCode()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await CreateAccountAsync(client, "Conto corrente");

        var response = await client.PostAsJsonAsync("/api/samples/accounts", new NameRequest("conto CORRENTE"), Ct);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, ApiErrors.AccountNameTaken);
    }

    // ---- Versions ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateAndRead_SendTheVersionAsETag()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        var created = await CreateAccountAsync(client, "Contanti");
        var read = await client.GetAsync($"/api/samples/accounts/{created.Id}", Ct);

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(EntityTag.Format(created.Version), read.Headers.ETag?.ToString());
        Assert.Equal(created.Version, (await read.Content.ReadFromJsonAsync<AccountResponse>(Ct))!.Version);
    }

    [Fact]
    public async Task UpdateWithoutIfMatch_Returns428()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var created = await CreateAccountAsync(client, "Risparmi");

        var response = await client.PutAsJsonAsync($"/api/samples/accounts/{created.Id}", new NameRequest("Nuovo"), Ct);

        await AssertProblemAsync(response, HttpStatusCode.PreconditionRequired, ApiErrors.VersionRequired);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("W/\"1\"")]
    [InlineData("1")]
    [InlineData("\"1\", \"2\"")]
    public async Task UpdateWithAMalformedIfMatch_Returns400(string ifMatch)
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var created = await CreateAccountAsync(client, "Carta");

        var response = await client.SendAsync(Update(created.Id, "Nuovo", ifMatch), Ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, ApiErrors.VersionInvalid);
    }

    [Fact]
    public async Task UpdateWithAStaleVersion_Returns412_AndChangesNothing()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var created = await CreateAccountAsync(client, "Vecchio nome");

        var response = await client.SendAsync(Update(created.Id, "Nuovo nome", EntityTag.Format(created.Version + 1)), Ct);

        await AssertProblemAsync(response, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Equal("Vecchio nome", (await ReadAccountAsync(client, created.Id)).Name);
    }

    [Fact]
    public async Task UpdateWithTheCurrentVersion_Succeeds_AndReturnsTheNewVersion()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var created = await CreateAccountAsync(client, "Prima");

        var response = await client.SendAsync(Update(created.Id, "Dopo", EntityTag.Format(created.Version)), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<AccountResponse>(Ct))!;
        Assert.Equal("Dopo", updated.Name);
        Assert.NotEqual(created.Version, updated.Version);
        Assert.Equal(EntityTag.Format(updated.Version), response.Headers.ETag?.ToString());
    }

    [Fact]
    public async Task ChangeSavedByAnotherDeviceAfterTheCheck_Returns412()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var created = await CreateAccountAsync(client, "Telefono");

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/samples/accounts/{created.Id}/race");
        request.Headers.TryAddWithoutValidation("If-Match", EntityTag.Format(created.Version));
        var response = await client.SendAsync(request, Ct);

        await AssertProblemAsync(response, HttpStatusCode.PreconditionFailed, ApiErrors.VersionMismatch);
        Assert.Equal("Computer", (await ReadAccountAsync(client, created.Id)).Name);
    }

    // ---- Another user's data -----------------------------------------------------------------------------------

    [Fact]
    public async Task AnotherUsersResource_Returns404_LikeANonexistentOne()
    {
        var otherAccountId = await CreateAccountOfAnotherUserAsync();
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        var other = await client.GetAsync($"/api/samples/accounts/{otherAccountId}", Ct);
        var nonexistent = await client.GetAsync($"/api/samples/accounts/{Guid.CreateVersion7()}", Ct);

        await AssertProblemAsync(other, HttpStatusCode.NotFound, ApiErrors.NotFound);
        await AssertProblemAsync(nonexistent, HttpStatusCode.NotFound, ApiErrors.NotFound);
    }

    [Fact]
    public async Task UpdatingAnotherUsersResource_Returns404_EvenWithAVersion()
    {
        var otherAccountId = await CreateAccountOfAnotherUserAsync();
        await using var app = await StartAsync();

        var response = await app.GetTestClient().SendAsync(Update(otherAccountId, "Mio", EntityTag.Format(1)), Ct);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, ApiErrors.NotFound);
    }

    [Fact]
    public async Task ReferenceToAnotherUsersData_Returns400OnTheField_LikeANonexistentId()
    {
        var otherAccountId = await CreateAccountOfAnotherUserAsync();
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        foreach (var accountId in new[] { otherAccountId, Guid.CreateVersion7() })
        {
            var response = await client.PostAsJsonAsync("/api/samples/references", new ReferenceRequest(accountId), Ct);

            var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "account.not_found");
            Assert.True(problem.GetProperty("errors").TryGetProperty("accountId", out _));
        }
    }

    // ---- Host and helpers --------------------------------------------------------------------------------------

    private const string DomainMessage = "Domain message for the client.";
    private const string SecretMessage = "Internal detail that must stay on the server.";

    private async Task<WebApplication> StartAsync()
    {
        var userId = await Database.CreateUserAsync(Ct);

        // Production: error responses must not show internal details there.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer();
        builder.Services.AddPersistence(Database.ConnectionString);
        builder.Services.AddApiErrors();
        builder.Services.AddApiSecurity();

        // The samples act for one signed-in user without going through login (block 3).
        builder.Services.Replace(ServiceDescriptor.Scoped<ICurrentUser>(_ => new FixedCurrentUser(userId)));

        var app = builder.Build();
        app.UseApiErrors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapModules(EndpointDiscovery.FromTypes([typeof(SamplesModule), typeof(SampleEndpoints)]));

        await app.StartAsync(Ct);
        return app;
    }

    private async Task<Guid> CreateAccountOfAnotherUserAsync()
    {
        var otherUserId = await Database.CreateUserAsync(Ct);
        await using var db = Database.CreateContext(otherUserId);
        var account = new Account(otherUserId, "Conto di un altro", AccountType.Checking, "EUR", 0m);
        db.Accounts.Add(account);
        await db.SaveChangesAsync(Ct);
        return account.Id;
    }

    private static async Task<AccountResponse> CreateAccountAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/samples/accounts", new NameRequest(name), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var account = (await response.Content.ReadFromJsonAsync<AccountResponse>(Ct))!;
        Assert.Equal(EntityTag.Format(account.Version), response.Headers.ETag?.ToString());
        return account;
    }

    private static async Task<AccountResponse> ReadAccountAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<AccountResponse>($"/api/samples/accounts/{id}", Ct))!;

    private static HttpRequestMessage Update(Guid id, string name, string ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/samples/accounts/{id}")
        {
            Content = JsonContent.Create(new NameRequest(name)),
        };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return request;
    }

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        return problem;
    }

    public sealed record NameRequest(string Name);

    public sealed record ReferenceRequest(Guid AccountId);

    public sealed record AccountResponse(Guid Id, string Name, uint Version);

    private sealed class SamplesModule : ApiModule
    {
        public override string Prefix => "samples";

        public override string Tag => "Samples";
    }

    private sealed class SampleEndpoints : IEndpoint<SamplesModule>
    {
        public void Map(IEndpointRouteBuilder module)
        {
            module.MapGet("domain/{code}", (string code) => ThrowDomain(code)).AllowAnonymous();
            module.MapGet("crash", () => ThrowUnexpected()).AllowAnonymous();
            module.MapGet("missing", () => TypedResults.NotFound()).AllowAnonymous();
            module.MapGet("private", () => "never reached without a session");
            module.MapPost("accounts", CreateAsync).AllowAnonymous();
            module.MapGet("accounts/{id:guid}", ReadAsync).AllowAnonymous();
            module.MapPut("accounts/{id:guid}", UpdateAsync).AllowAnonymous();
            module.MapPut("accounts/{id:guid}/race", UpdateWithRaceAsync).AllowAnonymous();
            module.MapPost("references", ReferenceAsync).AllowAnonymous();
        }

        private static string ThrowDomain(string code) => throw new DomainException(code, DomainMessage);

        private static string ThrowUnexpected() => throw new InvalidOperationException(SecretMessage);

        private static async Task<Created<AccountResponse>> CreateAsync(
            NameRequest request, AppDbContext db, ICurrentUser user, HttpResponse response, CancellationToken ct)
        {
            var account = new Account(user.UserId!.Value, request.Name, AccountType.Cash, "EUR", 0m);
            db.Accounts.Add(account);
            await db.SaveChangesAsync(ct);

            var version = RowVersion.Of(db, account);
            response.SetVersion(version);
            return TypedResults.Created($"/api/samples/accounts/{account.Id}", new AccountResponse(account.Id, account.Name, version));
        }

        private static async Task<Results<Ok<AccountResponse>, NotFound>> ReadAsync(
            Guid id, AppDbContext db, HttpResponse response, CancellationToken ct)
        {
            var account = await db.Accounts
                .Where(candidate => candidate.Id == id)
                .Select(candidate => new AccountResponse(candidate.Id, candidate.Name, EF.Property<uint>(candidate, RowVersion.PropertyName)))
                .SingleOrDefaultAsync(ct);
            if (account is null)
            {
                return TypedResults.NotFound();
            }

            response.SetVersion(account.Version);
            return TypedResults.Ok(account);
        }

        private static async Task<Results<Ok<AccountResponse>, NotFound>> UpdateAsync(
            Guid id, NameRequest request, IfMatch ifMatch, AppDbContext db, HttpResponse response, CancellationToken ct)
        {
            var account = await db.Accounts.SingleOrDefaultAsync(candidate => candidate.Id == id, ct);
            if (account is null)
            {
                return TypedResults.NotFound();
            }

            db.EnsureVersion(account, ifMatch);
            account.Rename(request.Name);
            await db.SaveChangesAsync(ct);

            var version = RowVersion.Of(db, account);
            response.SetVersion(version);
            return TypedResults.Ok(new AccountResponse(account.Id, account.Name, version));
        }

        // Another device saves between the version check and SaveChanges: EF Core's xmin check must refuse the write.
        private static async Task<Ok> UpdateWithRaceAsync(
            Guid id, IfMatch ifMatch, AppDbContext db, DbContextOptions<AppDbContext> options, ICurrentUser user, CancellationToken ct)
        {
            var account = await db.Accounts.SingleAsync(candidate => candidate.Id == id, ct);
            db.EnsureVersion(account, ifMatch);

            await using (var otherDevice = new AppDbContext(options, new FixedCurrentUser(user.UserId)))
            {
                var same = await otherDevice.Accounts.SingleAsync(candidate => candidate.Id == id, ct);
                same.Rename("Computer");
                await otherDevice.SaveChangesAsync(ct);
            }

            account.Rename("Telefono, salvato dopo");
            await db.SaveChangesAsync(ct);
            return TypedResults.Ok();
        }

        private static async Task<Ok> ReferenceAsync(ReferenceRequest request, AppDbContext db, CancellationToken ct)
        {
            await db.Accounts.GetReferenceAsync(request.AccountId, "account", "accountId", ct);
            return TypedResults.Ok();
        }
    }
}
