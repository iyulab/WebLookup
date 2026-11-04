using System.Net;

namespace WebLookup;

/// <summary>
/// Searches DuckDuckGo's HTML page. No API key is needed, but the page is scraped: when DuckDuckGo takes the client for a bot it answers with a challenge page, which this provider reports as an exception.
/// </summary>
public sealed class DuckDuckGoSearchProvider : SearchProviderBase
{
    private const string Endpoint = "https://html.duckduckgo.com/html/";

    private readonly DuckDuckGoSearchOptions _options;

    /// <summary>
    /// The provider's name, <c>"DuckDuckGo"</c>; it is also the <see cref="SearchResult.Provider"/> of every result it returns.
    /// </summary>
    public override string Name => "DuckDuckGo";

    /// <summary>
    /// Creates a provider with default options and its own HTTP client (disposed with the provider).
    /// </summary>
    public DuckDuckGoSearchProvider()
    {
        _options = new DuckDuckGoSearchOptions();
    }

    /// <summary>
    /// Creates the provider with its own HTTP client (disposed with the provider).
    /// </summary>
    /// <param name="options">The <see cref="DuckDuckGoSearchOptions"/> to use.</param>
    public DuckDuckGoSearchProvider(DuckDuckGoSearchOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Creates the provider on a caller's HTTP client, which the provider does not dispose.
    /// </summary>
    /// <param name="options">The <see cref="DuckDuckGoSearchOptions"/> to use.</param>
    /// <param name="httpClient">The client every request goes through.</param>
    public DuckDuckGoSearchProvider(DuckDuckGoSearchOptions options, HttpClient httpClient)
    {
        _options = options;
        SetHttpClient(httpClient);
    }

    /// <summary>
    /// Searches DuckDuckGo.
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

        var formFields = new List<KeyValuePair<string, string>>
        {
            new("q", query)
        };

        if (!string.IsNullOrEmpty(_options.Region))
        {
            formFields.Add(new("kl", _options.Region));
        }

        using var content = new FormUrlEncodedContent(formFields);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = content
        };
        request.Headers.Add("Referer", "https://html.duckduckgo.com/");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var results = DuckDuckGoHtmlParser.Parse(html);

        // DuckDuckGo answers a search it takes for automated with a challenge page (HTTP 202, or a page with an "anomaly"
        // dialog) instead of results. That is a refused search, like a 403 or a 429, not a search with no hits.
        if (results.Count == 0 && (response.StatusCode == HttpStatusCode.Accepted || DuckDuckGoHtmlParser.IsChallengePage(html)))
        {
            throw new HttpRequestException(
                "DuckDuckGo answered with its bot-challenge page instead of results, so the search was blocked. "
                + "Try again later, or use a provider that takes an API key.",
                inner: null,
                response.StatusCode);
        }

        return results.Count <= count
            ? results
            : results.Take(count).ToList();
    }
}
