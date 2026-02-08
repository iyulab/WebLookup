namespace WebLookup;

public record SitemapEntry
{
    public required string Url { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public string? ChangeFrequency { get; init; }
    public double? Priority { get; init; }
}
