using System.Text.Json;

namespace WebLookup;

public sealed class SearchApiProvider : SearchProviderBase
{
    private readonly SearchApiOptions _options;

    public override string Name => "SearchApi";

    public SearchApiProvider(SearchApiOptions options)
    {
        _options = options;
    }

    public SearchApiProvider(SearchApiOptions options, HttpClient httpClient)
    {
        _options = options;
        SetHttpClient(httpClient);
    }

    public override async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
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
