using System.Text.Json;
using WebLookup.Http;

namespace WebLookup;

/// <summary>
/// A base class for providers: it owns the HTTP client and offers small helpers for JSON APIs. Disposing the provider disposes a client it created, never one it was given.
/// </summary>
public abstract class SearchProviderBase : ISearchProvider, IDisposable
{
    private HttpClient? _httpClient;
    private bool _ownsClient;

    /// <summary>
    /// The provider's name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// The client every request goes through: the one given to <see cref="SetHttpClient"/>, or a default client created on first use.
    /// </summary>
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

    /// <summary>
    /// Makes the provider use a caller's client, which it will not dispose.
    /// </summary>
    /// <param name="httpClient">The client to use.</param>
    protected void SetHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _ownsClient = false;
    }

    /// <summary>
    /// Searches for <paramref name="query"/>.
    /// </summary>
    /// <param name="query">The search text; must contain something other than white space.</param>
    /// <param name="count">The most results to return, at least 1; a provider may return fewer.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The results, best first.</returns>
    /// <exception cref="ArgumentException"><paramref name="query"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 1.</exception>
    public abstract Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int count = 10,
        CancellationToken cancellationToken = default);

    /// <summary>Checks the arguments every provider's <c>SearchAsync</c> takes: a query with text, and a count of at least 1.</summary>
    protected static void ValidateSearchArguments(string query, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
    }

    /// <summary>
    /// Sends a GET request and parses the JSON body.
    /// </summary>
    /// <param name="client">The client to send with.</param>
    /// <param name="url">The request address.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The root element of the response.</returns>
    /// <exception cref="HttpRequestException">The response status is not a success.</exception>
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

    /// <summary>
    /// Sends a POST request with a JSON body and parses the JSON response.
    /// </summary>
    /// <param name="client">The client to send with.</param>
    /// <param name="url">The request address.</param>
    /// <param name="body">The object serialized as the request body.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The root element of the response.</returns>
    /// <exception cref="HttpRequestException">The response status is not a success.</exception>
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

    /// <summary>
    /// Reads a string property.
    /// </summary>
    /// <param name="element">The JSON object.</param>
    /// <param name="propertyName">The property to read.</param>
    /// <returns>The value, or <see langword="null"/> when the property is missing or not a string.</returns>
    protected static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    /// <summary>
    /// Disposes the HTTP client when this provider created it.
    /// </summary>
    public void Dispose()
    {
        if (_ownsClient)
            _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
