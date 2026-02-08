using System.Text.Json;

namespace WebLookup;

public sealed class MojeekSearchProvider : SearchProviderBase
{
    private readonly MojeekSearchOptions _options;

    public override string Name => "Mojeek";

    public MojeekSearchProvider(MojeekSearchOptions options)
    {
        _options = options;
    }

    public MojeekSearchProvider(MojeekSearchOptions options, HttpClient httpClient)
    {
        _options = options;
        SetHttpClient(httpClient);
    }

    public override async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        var url = $"https://www.mojeek.com/search?api_key={Uri.EscapeDataString(_options.ApiKey)}"
            + $"&q={Uri.EscapeDataString(query)}"
            + $"&t={count}"
            + "&fmt=json";

        var root = await GetJsonAsync(HttpClient, url, cancellationToken);

        var results = new List<SearchResult>();

        if (root.TryGetProperty("response", out var response) &&
            response.TryGetProperty("results", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var itemUrl = GetString(item, "url");
                var title = GetString(item, "title");
                if (itemUrl is null || title is null)
                    continue;

                results.Add(new SearchResult
                {
                    Url = itemUrl,
                    Title = title,
                    Description = GetString(item, "desc"),
                    Provider = Name
                });
            }
        }

        return results;
    }
}
