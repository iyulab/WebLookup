using System.Text.Json;

namespace WebLookup;

/// <summary>
/// Searches Google Custom Search. Every configured engine is queried in parallel (at most 10 results each) and the results are merged without duplicates; an engine that fails contributes nothing.
/// </summary>
public sealed class GoogleSearchProvider : SearchProviderBase
{
    private readonly GoogleSearchOptions _options;

    /// <summary>
    /// The provider's name, <c>"Google"</c>; it is also the <see cref="SearchResult.Provider"/> of every result it returns.
    /// </summary>
    public override string Name => "Google";

    /// <summary>
    /// Creates the provider with its own HTTP client (disposed with the provider).
    /// </summary>
    /// <param name="options">The <see cref="GoogleSearchOptions"/> to use.</param>
    public GoogleSearchProvider(GoogleSearchOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Creates the provider on a caller's HTTP client, which the provider does not dispose.
    /// </summary>
    /// <param name="options">The <see cref="GoogleSearchOptions"/> to use.</param>
    /// <param name="httpClient">The client every request goes through.</param>
    public GoogleSearchProvider(GoogleSearchOptions options, HttpClient httpClient)
    {
        _options = options;
        SetHttpClient(httpClient);
    }

    /// <summary>
    /// Searches every configured engine.
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

        var num = Math.Min(count, 10);
        var encodedQuery = Uri.EscapeDataString(query);

        var tasks = _options.Engines.Select(engine =>
            SearchEngineAsync(engine, encodedQuery, num, cancellationToken));

        var engineResults = await Task.WhenAll(tasks);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<SearchResult>();

        foreach (var batch in engineResults)
        {
            foreach (var result in batch)
            {
                if (seen.Add(result.Url))
                    results.Add(result);
            }
        }

        return results;
    }

    private async Task<List<SearchResult>> SearchEngineAsync(
        GoogleSearchEngine engine,
        string encodedQuery,
        int num,
        CancellationToken cancellationToken)
    {
        var results = new List<SearchResult>();

        try
        {
            var url = $"https://www.googleapis.com/customsearch/v1"
                + $"?key={Uri.EscapeDataString(engine.ApiKey)}"
                + $"&cx={Uri.EscapeDataString(engine.Cx)}"
                + $"&q={encodedQuery}"
                + $"&num={num}";

            var root = await GetJsonAsync(HttpClient, url, cancellationToken);

            if (root.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var link = GetString(item, "link");
                    var title = GetString(item, "title");
                    if (link is null || title is null)
                        continue;

                    results.Add(new SearchResult
                    {
                        Url = link,
                        Title = title,
                        Description = GetString(item, "snippet"),
                        Provider = Name
                    });
                }
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Individual engine failure: skip and let other engines provide results
        }

        return results;
    }
}
