namespace WebLookup;

public class MojeekSearchOptions
{
    public required string ApiKey { get; set; }
}

public class SearchApiOptions
{
    public required string ApiKey { get; set; }
    public string Engine { get; set; } = "google";
}

public class TavilySearchOptions
{
    public required string ApiKey { get; set; }
}

public class GoogleSearchOptions
{
    public IReadOnlyList<GoogleSearchEngine> Engines { get; set; } = [];
}

public class GoogleSearchEngine
{
    public required string ApiKey { get; set; }
    public required string Cx { get; set; }
}
