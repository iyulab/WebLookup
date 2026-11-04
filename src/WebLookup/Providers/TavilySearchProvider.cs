using System.Text.Json;

namespace WebLookup;

/// <summary>
/// Searches the Tavily API.
/// </summary>
public sealed class TavilySearchProvider : SearchProviderBase
{
    private readonly TavilySearchOptions _options;

    /// <summary>
    /// The provider's name, <c>"Tavily"</c>; it is also the <see cref="SearchResult.Provider"/> of every result it returns.
    /// </summary>
    public override string Name => "Tavily";

    /// <summary>
    /// Creates the provider with its own HTTP client (disposed with the provider).
    /// </summary>
    /// <param name="options">The <see cref="TavilySearchOptions"/> to use.</param>
    public TavilySearchProvider(TavilySearchOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Creates the provider on a caller's HTTP client, which the provider does not dispose.
    /// </summary>
    /// <param name="options">The <see cref="TavilySearchOptions"/> to use.</param>
    /// <param name="httpClient">The client every request goes through.</param>
    public TavilySearchProvider(TavilySearchOptions options, HttpClient httpClient)
    {
        _options = options;
        SetHttpClient(httpClient);
    }

    /// <summary>
    /// Searches Tavily.
    /// </summary>
    /// <param name="query">The search text; must contain something other than white space.</param>
    /// <param name="count">The most results to return, at least 1; a provider may return fewer.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The results, best first; empty when the provider answered with no hits.</returns>
    /// <exception cref="ArgumentException"><paramref name="query"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 1.</exception>
    /// <exception cref="HttpRequestException">The provider refused the request (an HTTP error status, a rejected key, or - for DuckDuckGo - its bot-challenge page).</exception>
    public override async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        ValidateSearchArguments(query, count);

        var body = new
        {
            api_key = _options.ApiKey,
            query,
            max_results = count
        };

        var root = await PostJsonAsync(HttpClient, "https://api.tavily.com/search", body, cancellationToken);

        var results = new List<SearchResult>();

        if (root.TryGetProperty("results", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var url = GetString(item, "url");
                var title = GetString(item, "title");
                if (url is null || title is null)
                    continue;

                results.Add(new SearchResult
                {
                    Url = url,
                    Title = title,
                    Description = GetString(item, "content"),
                    Provider = Name
                });
            }
        }

        return results;
    }
}
