namespace WebLookup;

/// <summary>
/// One search hit.
/// </summary>
public record SearchResult
{
    /// <summary>
    /// The address of the page.
    /// </summary>
    public required string Url { get; init; }
    /// <summary>
    /// The page's title.
    /// </summary>
    public required string Title { get; init; }
    /// <summary>
    /// A snippet or summary from the provider; <see langword="null"/> when it gave none.
    /// </summary>
    public string? Description { get; init; }
    /// <summary>
    /// The name of the provider that returned the hit.
    /// </summary>
    public string? Provider { get; init; }
}
