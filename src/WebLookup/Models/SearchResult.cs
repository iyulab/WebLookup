namespace WebLookup;

public record SearchResult
{
    public required string Url { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? Provider { get; init; }
}
