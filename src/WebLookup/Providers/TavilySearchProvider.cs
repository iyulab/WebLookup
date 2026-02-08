using System.Text.Json;

namespace WebLookup;

public sealed class TavilySearchProvider : SearchProviderBase
{
    private readonly TavilySearchOptions _options;

    public override string Name => "Tavily";

    public TavilySearchProvider(TavilySearchOptions options)
    {
        _options = options;
    }

    public TavilySearchProvider(TavilySearchOptions options, HttpClient httpClient)
    {
        _options = options;
        SetHttpClient(httpClient);
    }

    public override async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
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
