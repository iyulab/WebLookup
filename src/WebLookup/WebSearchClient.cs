using System.Runtime.ExceptionServices;

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

        var outcomes = await Task.WhenAll(tasks);

        // One provider failing leaves the others' results; every provider failing is a failed search, not a search with
        // no hits - an empty list would tell the caller the web has nothing on the query.
        if (outcomes.Length > 0 && outcomes.All(o => o.Error is not null))
        {
            var errors = outcomes.Select(o => o.Error!).ToArray();
            if (errors.Length == 1)
            {
                ExceptionDispatchInfo.Capture(errors[0]).Throw();
            }

            throw new AggregateException("Every search provider failed.", errors);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduplicated = new List<SearchResult>();

        foreach (var outcome in outcomes)
        {
            foreach (var result in outcome.Results)
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

    private static async Task<(IReadOnlyList<SearchResult> Results, Exception? Error)> SafeSearchAsync(
        ISearchProvider provider,
        string query,
        int count,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await provider.SearchAsync(query, count, cancellationToken), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Only the caller's own cancellation ends the search. A provider's HttpClient timeout is an
            // OperationCanceledException too, and is that provider failing.
            throw;
        }
        catch (Exception ex)
        {
            return ([], ex);
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
