namespace ScreepsDotNet.Backend.Http.Tests.TestSupport;

using System.Net.Http.Headers;
using System.Net.Http.Json;

internal sealed class TestHttpClient(HttpClient inner)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public HttpRequestHeaders DefaultRequestHeaders => inner.DefaultRequestHeaders;

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        => inner.SendAsync(request, Token);

    public Task<HttpResponseMessage> GetAsync(string requestUri)
        => inner.GetAsync(requestUri, Token);

    public Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content)
        => inner.PostAsync(requestUri, content, Token);

    public Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content)
        => inner.PutAsync(requestUri, content, Token);

    public Task<HttpResponseMessage> DeleteAsync(string requestUri)
        => inner.DeleteAsync(requestUri, Token);

    public Task<HttpResponseMessage> PostAsJsonAsync<T>(string? requestUri, T value)
        => inner.PostAsJsonAsync(requestUri, value, Token);

    public Task<HttpResponseMessage> PutAsJsonAsync<T>(string? requestUri, T value)
        => inner.PutAsJsonAsync(requestUri, value, Token);

    public static Task<string> ReadAsStringAsync(HttpResponseMessage response)
        => response.Content.ReadAsStringAsync(Token);

    public static Task<T?> ReadFromJsonAsync<T>(HttpResponseMessage response)
        => response.Content.ReadFromJsonAsync<T>(cancellationToken: Token);
}
