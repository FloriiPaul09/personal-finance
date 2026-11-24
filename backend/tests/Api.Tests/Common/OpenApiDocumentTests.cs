using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Tests.Support;

namespace PersonalFinance.Api.Tests.Common;

// The OpenAPI document is precise enough to generate the client's types without corrections (ADR-0029,
// docs/backend/api-endpoints.md#openapi). The document written at build time comes from the same generator.
public sealed class OpenApiDocumentTests(ApiServer server) : ApiTest(server)
{
    private static readonly string[] WriteMethods = ["put", "patch", "delete"];

    [Fact]
    public async Task Numbers_AreDescribedAsNumbersOnly()
    {
        var document = await DocumentAsync();

        // The web JSON defaults would publish "number | string" with a pattern (numbers read from text).
        var loose = Schemas(document)
            .Where(schema => schema.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.Array
                && type.EnumerateArray().Any(t => t.GetString() is "number" or "integer")
                && type.EnumerateArray().Any(t => t.GetString() == "string"))
            .ToList();
        Assert.Empty(loose);

        var account = Component(document, "AccountResponse").GetProperty("properties");
        Assert.Equal("number", account.GetProperty("balance").GetProperty("type").GetString());
        Assert.Equal("integer", account.GetProperty("version").GetProperty("type").GetString());
        Assert.False(account.GetProperty("balance").TryGetProperty("pattern", out _));
    }

    [Fact]
    public async Task Months_AreYyyyMmStrings_InBodiesAndQueries()
    {
        var document = await DocumentAsync();

        var month = Component(document, "YearMonth");
        Assert.Equal("string", month.GetProperty("type").GetString());
        Assert.Equal(@"^\d{4}-(0[1-9]|1[0-2])$", month.GetProperty("pattern").GetString());
        // OpenAPI 3.1 / JSON Schema form: "examples" (a list); "example" is obsolete in Microsoft.OpenApi 2.13.
        Assert.Equal("2026-06", Assert.Single(month.GetProperty("examples").EnumerateArray()).GetString());
        Assert.False(month.TryGetProperty("example", out _));

        Assert.Equal("#/components/schemas/YearMonth", RefOf(Component(document, "BudgetResponse").GetProperty("properties").GetProperty("from")));

        foreach (var path in new[] { "/api/budgets/progress", "/api/dashboard" })
        {
            var parameter = Operation(document, path, "get").GetProperty("parameters").EnumerateArray()
                .Single(p => p.GetProperty("name").GetString() == "month");
            Assert.Equal("query", parameter.GetProperty("in").GetString());
            Assert.Equal("#/components/schemas/YearMonth", RefOf(parameter.GetProperty("schema")));
        }
    }

    [Fact]
    public async Task IfMatch_IsRequired_ExactlyWhereTheEndpointTakesAVersion()
    {
        var document = await DocumentAsync();

        var expected = Host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IfMatchRequiredMetadata>() is not null)
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()!.EndpointName)
            .Order(StringComparer.Ordinal)
            .ToList();

        var documented = Operations(document)
            .Where(operation => Parameters(operation).Any(p =>
                p.GetProperty("in").GetString() == "header" && p.GetProperty("name").GetString() == "If-Match"))
            .Select(operation => operation.GetProperty("operationId").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, documented);
    }

    [Fact]
    public async Task EveryUpdateAndDelete_DescribesIfMatchAndItsErrors()
    {
        var document = await DocumentAsync();

        foreach (var (path, method, operation) in OperationsWithPaths(document).Where(o => WriteMethods.Contains(o.Method)))
        {
            var ifMatch = Parameters(operation).Single(p => p.GetProperty("name").GetString() == "If-Match");
            Assert.True(ifMatch.GetProperty("required").GetBoolean(), $"{method} {path}");
            Assert.Equal("header", ifMatch.GetProperty("in").GetString());

            foreach (var status in new[] { "400", "412", "428" })
            {
                var response = operation.GetProperty("responses").GetProperty(status);
                Assert.Equal(
                    "#/components/schemas/ProblemDetails",
                    RefOf(response.GetProperty("content").GetProperty("application/problem+json").GetProperty("schema")));
            }
        }
    }

    [Theory]
    [InlineData("/api/accounts/{id}", "get", "200")]
    [InlineData("/api/accounts", "post", "201")]
    [InlineData("/api/accounts/{id}", "put", "200")]
    [InlineData("/api/accounts/{id}/archive", "post", "200")]
    [InlineData("/api/transactions", "post", "201")]
    [InlineData("/api/transactions", "post", "200")]
    [InlineData("/api/transfers/{id}", "get", "200")]
    [InlineData("/api/budgets/{id}", "put", "200")]
    [InlineData("/api/tags/{id}", "put", "200")]
    public async Task SingleResources_DescribeTheirETag(string path, string method, string status)
    {
        var document = await DocumentAsync();

        var etag = Operation(document, path, method).GetProperty("responses").GetProperty(status).GetProperty("headers").GetProperty("ETag");

        Assert.Equal(@"^""\d+""$", etag.GetProperty("schema").GetProperty("pattern").GetString());
        Assert.Equal(@"""12345""", Assert.Single(etag.GetProperty("schema").GetProperty("examples").EnumerateArray()).GetString());
    }

    [Theory]
    [InlineData("/api/accounts", "get")]
    [InlineData("/api/transactions", "get")]
    [InlineData("/api/dashboard", "get")]
    [InlineData("/api/auth/me", "get")]
    public async Task ListsAndOtherResponses_HaveNoETag(string path, string method)
    {
        var document = await DocumentAsync();

        var ok = Operation(document, path, method).GetProperty("responses").GetProperty("200");

        Assert.False(ok.TryGetProperty("headers", out var headers) && headers.TryGetProperty("ETag", out _));
    }

    [Fact]
    public async Task EveryDocumentedETag_ComesFromAHandlerThatCanSendIt()
    {
        var document = await DocumentAsync();
        var handlers = Host.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>() is not null)
            .ToDictionary(
                endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()!.EndpointName,
                endpoint => endpoint.Metadata.OfType<MethodInfo>().Single());

        var withETag = Operations(document)
            .Where(operation => operation.GetProperty("responses").EnumerateObject().Any(response =>
                response.Value.TryGetProperty("headers", out var headers) && headers.TryGetProperty("ETag", out _)))
            .Select(operation => operation.GetProperty("operationId").GetString()!)
            .ToList();

        // The ETag is described from the response type; the handler sends it through HttpResponse.SetVersion.
        Assert.NotEmpty(withETag);
        Assert.All(withETag, name =>
            Assert.Contains(handlers[name].GetParameters(), parameter => parameter.ParameterType == typeof(HttpResponse)));
    }

    [Fact]
    public async Task Errors_HaveOneSchema_WithCodeAndTraceId()
    {
        var document = await DocumentAsync();

        var problem = Component(document, "ProblemDetails");
        var required = problem.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToList();
        Assert.Contains("code", required);
        Assert.Contains("traceId", required);
        Assert.Equal("array", problem.GetProperty("properties").GetProperty("errors").GetProperty("additionalProperties").GetProperty("type").GetString());

        // No servers: the document written at build time and the served one are the same.
        Assert.False(document.TryGetProperty("servers", out var servers) && servers.GetArrayLength() > 0);
    }

    private async Task<JsonElement> DocumentAsync() =>
        await AnonymousClient().GetJsonAsync<JsonElement>("/openapi/v1.json");

    private static JsonElement Component(JsonElement document, string name) =>
        document.GetProperty("components").GetProperty("schemas").GetProperty(name);

    private static JsonElement Operation(JsonElement document, string path, string method) =>
        document.GetProperty("paths").GetProperty(path).GetProperty(method);

    private static IEnumerable<(string Path, string Method, JsonElement Operation)> OperationsWithPaths(JsonElement document) =>
        document.GetProperty("paths").EnumerateObject().SelectMany(path => path.Value.EnumerateObject()
            .Where(entry => entry.Value.ValueKind == JsonValueKind.Object && entry.Value.TryGetProperty("responses", out _))
            .Select(entry => (path.Name, entry.Name, entry.Value)));

    private static IEnumerable<JsonElement> Operations(JsonElement document) =>
        OperationsWithPaths(document).Select(operation => operation.Operation);

    private static IEnumerable<JsonElement> Parameters(JsonElement operation) =>
        operation.TryGetProperty("parameters", out var parameters) ? parameters.EnumerateArray() : [];

    private static string? RefOf(JsonElement schema) =>
        schema.TryGetProperty("$ref", out var reference) ? reference.GetString() : null;

    /// <summary>Every object in the document: schemas are nested anywhere (properties, items, parameters).</summary>
    private static IEnumerable<JsonElement> Schemas(JsonElement node) => node.ValueKind switch
    {
        JsonValueKind.Object => new[] { node }.Concat(node.EnumerateObject().SelectMany(property => Schemas(property.Value))),
        JsonValueKind.Array => node.EnumerateArray().SelectMany(Schemas),
        _ => [],
    };
}
