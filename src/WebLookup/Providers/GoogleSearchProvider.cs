using System.Text.Json;

namespace WebLookup;

public sealed class GoogleSearchProvider : SearchProviderBase
{
    private readonly GoogleSearchOptions _options;

    public override string Name => "Google";

    public GoogleSearchProvider(GoogleSearchOptions options)
    {
        _options = options;
    }

    public GoogleSearchProvider(GoogleSearchOptions options, HttpClient httpClient)
    {
        _options = options;
        SetHttpClient(httpClient);
    }

    public override async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
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
