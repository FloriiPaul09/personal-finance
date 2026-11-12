using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PersonalFinance.Api.Tests.Support;

/// <summary>Assertions on error responses (docs/backend/validation-and-errors.md#response-shape).</summary>
public static class Problems
{
    /// <summary>Checks status, content type, <c>code</c> and <c>traceId</c>; returns the body for further checks.</summary>
    public static async Task<JsonElement> AssertAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        return problem;
    }

    /// <summary>The problem reports an error on <paramref name="field"/> (field names compared without case).</summary>
    public static void AssertField(JsonElement problem, string field) =>
        Assert.Contains(
            problem.GetProperty("errors").EnumerateObject(),
            error => string.Equals(error.Name, field, StringComparison.OrdinalIgnoreCase));
}
