using System.Net;

namespace WebLookup.Tests;

public class IntegrationTests
{
    private static readonly string Query = "dotnet web search library";

    private static string? GetEnv(string key)
    {
        var envFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env");
        if (File.Exists(envFile))
        {
            foreach (var line in File.ReadAllLines(envFile))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                    continue;
                var eq = trimmed.IndexOf('=');
                if (eq < 0) continue;
                var k = trimmed[..eq].Trim();
                var v = trimmed[(eq + 1)..].Trim();
                if (k == key && !string.IsNullOrEmpty(v))
                    return v;
            }
        }
        return Environment.GetEnvironmentVariable(key);
    }

    private static bool IsAuthError(HttpRequestException ex)
    {
        return ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
    }

    [Fact]
    public async Task Mojeek_RealSearch()
    {
        var apiKey = GetEnv("MOJEEK_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            return;

        var provider = new MojeekSearchProvider(new MojeekSearchOptions { ApiKey = apiKey });

        IReadOnlyList<SearchResult> results;
        try
        {
            results = await provider.SearchAsync(Query, count: 3);
        }
        catch (HttpRequestException ex) when (IsAuthError(ex))
        {
            return; // Skip: invalid API key
        }

        Assert.NotEmpty(results);
        Assert.All(results, r =>
        {
            Assert.False(string.IsNullOrEmpty(r.Url));
            Assert.False(string.IsNullOrEmpty(r.Title));
            Assert.Equal("Mojeek", r.Provider);
        });
    }

    [Fact]
    public async Task SearchApi_RealSearch()
    {
        var apiKey = GetEnv("SEARCHAPI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            return;

        var engine = GetEnv("SEARCHAPI_ENGINE") ?? "google";
        var provider = new SearchApiProvider(new SearchApiOptions
        {
            ApiKey = apiKey,
            Engine = engine
        });

        IReadOnlyList<SearchResult> results;
        try
        {
            results = await provider.SearchAsync(Query, count: 3);
        }
        catch (HttpRequestException ex) when (IsAuthError(ex))
        {
            return; // Skip: invalid API key
        }

        Assert.NotEmpty(results);
        Assert.All(results, r =>
        {
            Assert.False(string.IsNullOrEmpty(r.Url));
            Assert.False(string.IsNullOrEmpty(r.Title));
            Assert.Equal("SearchApi", r.Provider);
        });
    }

    [Fact]
    public async Task Tavily_RealSearch()
    {
        var apiKey = GetEnv("TAVILY_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            return;

        var provider = new TavilySearchProvider(new TavilySearchOptions { ApiKey = apiKey });

        IReadOnlyList<SearchResult> results;
        try
        {
            results = await provider.SearchAsync(Query, count: 3);
        }
        catch (HttpRequestException ex) when (IsAuthError(ex))
        {
            return; // Skip: invalid API key
        }

        Assert.NotEmpty(results);
        Assert.All(results, r =>
        {
            Assert.False(string.IsNullOrEmpty(r.Url));
            Assert.False(string.IsNullOrEmpty(r.Title));
            Assert.Equal("Tavily", r.Provider);
        });
    }

    [Fact]
    public async Task DuckDuckGo_RealSearch()
    {
        var provider = new DuckDuckGoSearchProvider();

        IReadOnlyList<SearchResult> results;
        try
        {
            results = await provider.SearchAsync(Query, count: 3);
        }
        catch (HttpRequestException)
        {
            return; // Skip: network error
        }

        Assert.NotEmpty(results);
        Assert.All(results, r =>
        {
            Assert.False(string.IsNullOrEmpty(r.Url));
            Assert.False(string.IsNullOrEmpty(r.Title));
            Assert.Equal("DuckDuckGo", r.Provider);
        });
    }

    [Fact]
    public async Task WebSearchClient_MultiProvider_RealSearch()
    {
        var providers = new List<ISearchProvider>();

        // DuckDuckGo always participates (no API key needed)
        providers.Add(new DuckDuckGoSearchProvider());

        var mojeekKey = GetEnv("MOJEEK_API_KEY");
        if (!string.IsNullOrEmpty(mojeekKey))
            providers.Add(new MojeekSearchProvider(new MojeekSearchOptions { ApiKey = mojeekKey }));

        var searchApiKey = GetEnv("SEARCHAPI_API_KEY");
        if (!string.IsNullOrEmpty(searchApiKey))
            providers.Add(new SearchApiProvider(new SearchApiOptions
            {
                ApiKey = searchApiKey,
                Engine = GetEnv("SEARCHAPI_ENGINE") ?? "google"
            }));

        var tavilyKey = GetEnv("TAVILY_API_KEY");
        if (!string.IsNullOrEmpty(tavilyKey))
            providers.Add(new TavilySearchProvider(new TavilySearchOptions { ApiKey = tavilyKey }));

        using var client = new WebSearchClient([.. providers]);
        var results = await client.SearchAsync(Query);

        if (results.Count == 0)
            return; // All providers may have invalid keys

        var providerNames = results.Select(r => r.Provider).Distinct().ToList();
        Assert.True(providerNames.Count >= 1);

        Assert.All(results, r =>
        {
            Assert.True(Uri.TryCreate(r.Url, UriKind.Absolute, out _), $"Invalid URL: {r.Url}");
            Assert.False(string.IsNullOrEmpty(r.Title));
        });
    }
}
