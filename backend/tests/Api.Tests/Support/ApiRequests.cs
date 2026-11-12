using System.Net.Http.Json;
using PersonalFinance.Api.Common.Concurrency;
using PersonalFinance.Api.Common.Json;

namespace PersonalFinance.Api.Tests.Support;

/// <summary>Requests and responses as the app sends and reads them: API JSON conventions, versions in <c>If-Match</c>.</summary>
public static class ApiRequests
{
    /// <summary>The body of a successful response, read with the JSON conventions of the API (enumerations as strings).</summary>
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<T>(JsonConventions.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        return body;
    }

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string uri)
    {
        var response = await client.GetAsync(uri, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<T>();
    }

    /// <summary><c>PUT</c> with <paramref name="version"/> in <c>If-Match</c>, like the app after reading the resource.</summary>
    public static Task<HttpResponseMessage> PutWithVersionAsync(this HttpClient client, string uri, object body, uint version) =>
        client.SendAsync(WithVersion(HttpMethod.Put, uri, version, body), TestContext.Current.CancellationToken);

    /// <summary><c>DELETE</c> with <paramref name="version"/> in <c>If-Match</c>.</summary>
    public static Task<HttpResponseMessage> DeleteWithVersionAsync(this HttpClient client, string uri, uint version) =>
        client.SendAsync(WithVersion(HttpMethod.Delete, uri, version), TestContext.Current.CancellationToken);

    public static HttpRequestMessage WithVersion(HttpMethod method, string uri, uint version, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.TryAddWithoutValidation("If-Match", EntityTag.Format(version));
        return request;
    }

    /// <summary>A POST without body, e.g. an action like <c>archive</c>.</summary>
    public static Task<HttpResponseMessage> PostEmptyAsync(this HttpClient client, string uri) =>
        client.PostAsync(uri, content: null, TestContext.Current.CancellationToken);
}
