namespace WebLookup;

public interface ISearchProvider
{
    string Name { get; }

    Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int count = 10,
        CancellationToken cancellationToken = default);
}
