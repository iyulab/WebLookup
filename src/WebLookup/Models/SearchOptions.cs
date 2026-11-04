namespace WebLookup;

/// <summary>
/// Options for one <see cref="WebSearchClient"/> search.
/// </summary>
public class WebSearchOptions
{
    /// <summary>
    /// The most results to ask each provider for (default 10); at least 1.
    /// </summary>
    public int MaxResultsPerProvider { get; set; } = 10;
}
