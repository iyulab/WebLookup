using System.Text.Json;
using WebLookup.Http;

namespace WebLookup;

public abstract class SearchProviderBase : ISearchProvider, IDisposable
{
    private HttpClient? _httpClient;
    private bool _ownsClient;

    public abstract string Name { get; }

    protected HttpClient HttpClient
    {
        get
        {
            if (_httpClient is null)
            {
                var handler = new RateLimitHandler(new HttpClientHandler());
                _httpClient = new HttpClient(handler);
                _ownsClient = true;
            }
            return _httpClient;
        }
    }

    protected void SetHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _ownsClient = false;
    }

    public abstract Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int count = 10,
        CancellationToken cancellationToken = default);

    protected static async Task<JsonElement> GetJsonAsync(
        HttpClient client,
        string url,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return doc.RootElement.Clone();
    }

    protected static async Task<JsonElement> PostJsonAsync(
        HttpClient client,
        string url,
        object body,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return doc.RootElement.Clone();
    }

    protected static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    public void Dispose()
    {
        if (_ownsClient)
            _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
