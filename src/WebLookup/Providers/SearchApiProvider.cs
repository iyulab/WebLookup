using System.Text.Json;

namespace WebLookup;

/// <summary>
/// Searches through SearchApi (searchapi.io).
/// </summary>
public sealed class SearchApiProvider : SearchProviderBase
{
    private readonly SearchApiOptions _options;

    /// <summary>
    /// The provider's name, <c>"SearchApi"</c>; it is also the <see cref="SearchResult.Provider"/> of every result it returns.
    /// </summary>
    public override string Name => "SearchApi";

    /// <summary>
    /// Creates the provider with its own HTTP client (disposed with the provider).
    /// </summary>
    /// <param name="options">The <see cref="SearchApiOptions"/> to use.</param>
    public SearchApiProvider(SearchApiOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Creates the provider on a caller's HTTP client, which the provider does not dispose.
    /// </summary>
    /// <param name="options">The <see cref="SearchApiOptions"/> to use.</param>
    /// <param name="httpClient">The client every request goes through.</param>
    public SearchApiProvider(SearchApiOptions options, HttpClient httpClient)
    {
        _options = options;
        SetHttpClient(httpClient);
    }

    /// <summary>
    /// Searches SearchApi.
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

        var engine = Uri.EscapeDataString(_options.Engine);
        var url = $"https://www.searchapi.io/api/v1/search?engine={engine}"
            + $"&q={Uri.EscapeDataString(query)}"
            + $"&num={count}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        var results = new List<SearchResult>();

        if (root.TryGetProperty("organic_results", out var organicResults))
        {
            foreach (var item in organicResults.EnumerateArray())
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

        return results;
    }
}
