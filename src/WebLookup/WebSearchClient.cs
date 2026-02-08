namespace WebLookup;

public sealed class WebSearchClient : IDisposable
{
    private readonly ISearchProvider[] _providers;

    public WebSearchOptions Options { get; set; } = new();

    public WebSearchClient(params ISearchProvider[] providers)
    {
        _providers = providers;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        return await SearchAsync(query, Options, cancellationToken);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        WebSearchOptions? options,
        CancellationToken cancellationToken = default)
    {
        options ??= Options;
        var count = options.MaxResultsPerProvider;

        var tasks = _providers.Select(provider =>
            SafeSearchAsync(provider, query, count, cancellationToken));

        var allResults = await Task.WhenAll(tasks);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduplicated = new List<SearchResult>();

        foreach (var resultSet in allResults)
        {
            foreach (var result in resultSet)
            {
                var normalized = NormalizeUrl(result.Url);
                if (seen.Add(normalized))
                {
                    deduplicated.Add(result);
                }
            }
        }

        return deduplicated;
    }

    private static async Task<IReadOnlyList<SearchResult>> SafeSearchAsync(
        ISearchProvider provider,
        string query,
        int count,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.SearchAsync(query, count, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return [];
        }
    }

    internal static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url.ToLowerInvariant();

        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        var path = uri.AbsolutePath.TrimEnd('/');
        var query = uri.Query;

        return $"{scheme}://{host}{port}{path}{query}";
    }

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            if (provider is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
