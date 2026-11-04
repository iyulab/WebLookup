namespace WebLookup;

/// <summary>
/// A source of web search results.
/// </summary>
public interface ISearchProvider
{
    /// <summary>
    /// The provider's name, which results carry in <see cref="SearchResult.Provider"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Searches for <paramref name="query"/>.
    /// </summary>
    /// <param name="query">The search text; must contain something other than white space.</param>
    /// <param name="count">The most results to return, at least 1; a provider may return fewer.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The results, best first; empty when the provider answered with no hits.</returns>
    /// <exception cref="ArgumentException"><paramref name="query"/> is null, empty or white space (the providers in this package).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 1 (the providers in this package).</exception>
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int count = 10,
        CancellationToken cancellationToken = default);
}
