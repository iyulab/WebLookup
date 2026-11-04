namespace WebLookup;

/// <summary>
/// Options for <see cref="MojeekSearchProvider"/>.
/// </summary>
public class MojeekSearchOptions
{
    /// <summary>
    /// The Mojeek API key.
    /// </summary>
    public required string ApiKey { get; set; }
}

/// <summary>
/// Options for <see cref="SearchApiProvider"/>.
/// </summary>
public class SearchApiOptions
{
    /// <summary>
    /// The SearchApi API key, sent as a bearer token.
    /// </summary>
    public required string ApiKey { get; set; }
    /// <summary>
    /// The SearchApi engine to query; <c>"google"</c> by default.
    /// </summary>
    public string Engine { get; set; } = "google";
}

/// <summary>
/// Options for <see cref="TavilySearchProvider"/>.
/// </summary>
public class TavilySearchOptions
{
    /// <summary>
    /// The Tavily API key.
    /// </summary>
    public required string ApiKey { get; set; }
}

/// <summary>
/// Options for <see cref="GoogleSearchProvider"/>.
/// </summary>
public class GoogleSearchOptions
{
    /// <summary>
    /// The Custom Search engines to query; they are searched in parallel and their results merged. An engine that fails is skipped.
    /// </summary>
    public IReadOnlyList<GoogleSearchEngine> Engines { get; set; } = [];
}

/// <summary>
/// One Google Custom Search engine.
/// </summary>
public class GoogleSearchEngine
{
    /// <summary>
    /// The API key.
    /// </summary>
    public required string ApiKey { get; set; }
    /// <summary>
    /// The search engine id (<c>cx</c>).
    /// </summary>
    public required string Cx { get; set; }
}

/// <summary>
/// Options for <see cref="DuckDuckGoSearchProvider"/>.
/// </summary>
public class DuckDuckGoSearchOptions
{
    /// <summary>
    /// The DuckDuckGo region code (the <c>kl</c> parameter, for example <c>"us-en"</c>); <see langword="null"/> for DuckDuckGo's default.
    /// </summary>
    public string? Region { get; set; }
}
