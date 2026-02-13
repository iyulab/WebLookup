namespace WebLookup;

public sealed class DuckDuckGoSearchProvider : SearchProviderBase
{
    private const string Endpoint = "https://html.duckduckgo.com/html/";

    private readonly DuckDuckGoSearchOptions _options;

    public override string Name => "DuckDuckGo";

    public DuckDuckGoSearchProvider()
    {
        _options = new DuckDuckGoSearchOptions();
    }

    public DuckDuckGoSearchProvider(DuckDuckGoSearchOptions options)
    {
        _options = options;
    }

    public DuckDuckGoSearchProvider(DuckDuckGoSearchOptions options, HttpClient httpClient)
    {
        _options = options;
        SetHttpClient(httpClient);
    }

    public override async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
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

        return results.Count <= count
            ? results
            : results.Take(count).ToList();
    }
}
