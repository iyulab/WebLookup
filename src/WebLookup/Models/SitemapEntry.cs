namespace WebLookup;

/// <summary>
/// One page listed in a sitemap.
/// </summary>
public record SitemapEntry
{
    /// <summary>
    /// The page's absolute <c>http</c> or <c>https</c> address, without surrounding white space.
    /// </summary>
    public required string Url { get; init; }
    /// <summary>
    /// The <c>lastmod</c> value; a value without a UTC offset is read as UTC. <see langword="null"/> when absent or not a date.
    /// </summary>
    public DateTimeOffset? LastModified { get; init; }
    /// <summary>
    /// The <c>changefreq</c> value as written; <see langword="null"/> when absent.
    /// </summary>
    public string? ChangeFrequency { get; init; }
    /// <summary>
    /// The <c>priority</c> value; <see langword="null"/> when absent or not a number.
    /// </summary>
    public double? Priority { get; init; }
}
