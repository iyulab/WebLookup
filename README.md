# WebLookup

[![NuGet](https://img.shields.io/nuget/v/WebLookup)](https://www.nuget.org/packages/WebLookup)
[![NuGet Downloads](https://img.shields.io/nuget/dt/WebLookup)](https://www.nuget.org/packages/WebLookup)
[![Build](https://github.com/iyulab/WebLookup/actions/workflows/publish.yml/badge.svg)](https://github.com/iyulab/WebLookup/actions/workflows/publish.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A lightweight .NET library for fast URL discovery across multiple search providers with built-in rate limiting, automatic fallback, and site exploration.

**WebLookup is a URL search engine, not a content parser.** It collects URLs and metadata (title, description) from search APIs and sitemaps, then hands them off to your crawler or parser of choice.

## Features

- **Multi-provider search** — DuckDuckGo (no API key), Google, Mojeek, SearchApi, Tavily
- **Parallel execution** — All configured providers queried simultaneously
- **Smart fallback** — Continues serving results from healthy providers when one fails or hits rate limits
- **URL deduplication** — Merges results across providers, removes duplicates
- **Site exploration** — Parse `robots.txt` rules and `sitemap.xml` hierarchies
- **Rate limit handling** — Auto-detects 429/Retry-After, exponential backoff per provider
- **Minimal dependencies** — Built on `System.Net.Http` and `System.Text.Json`; optional DI integration
- **DI-friendly** — First-class `Microsoft.Extensions.DependencyInjection` support

## Installation

```bash
dotnet add package WebLookup
```

## Quick Start

### Zero-config search (no API key needed)

```csharp
using WebLookup;

// DuckDuckGo requires no API key
var provider = new DuckDuckGoSearchProvider();
var results = await provider.SearchAsync("dotnet web search", count: 5);
```

### Search across multiple providers

```csharp
using WebLookup;

var client = new WebSearchClient(
    new DuckDuckGoSearchProvider(),  // No API key needed
    new GoogleSearchProvider(new() { Engines = [new() { ApiKey = "...", Cx = "..." }] }),
    new MojeekSearchProvider(new() { ApiKey = "..." }),
    new SearchApiProvider(new() { ApiKey = "..." }),
    new TavilySearchProvider(new() { ApiKey = "..." })
);

var results = await client.SearchAsync("dotnet web search library");

foreach (var item in results)
{
    Console.WriteLine($"[{item.Provider}] {item.Title}");
    Console.WriteLine($"  {item.Url}");
    Console.WriteLine($"  {item.Description}");
}
```

Results are deduplicated by URL. Providers run in parallel. If one provider hits a rate limit, results from others are still returned.

### Use a single provider

```csharp
// Google Custom Search
var google = new GoogleSearchProvider(new()
{
    Engines = [new() { ApiKey = "YOUR_API_KEY", Cx = "YOUR_CX" }]
});

var results = await google.SearchAsync("query", count: 5);
```

```csharp
// Tavily
var tavily = new TavilySearchProvider(new() { ApiKey = "YOUR_API_KEY" });

var results = await tavily.SearchAsync("query", count: 5);
```

### Explore a site

```csharp
var explorer = new SiteExplorer();

// Read robots.txt
var robots = await explorer.GetRobotsAsync(new Uri("https://example.com"));
Console.WriteLine($"Crawl-Delay: {robots.CrawlDelay}");
Console.WriteLine($"Sitemaps: {string.Join(", ", robots.Sitemaps)}");

foreach (var rule in robots.Rules)
{
    Console.WriteLine($"[{rule.UserAgent}] {rule.Type}: {rule.Path}");
}

// Read sitemap
var entries = await explorer.GetSitemapAsync(new Uri("https://example.com/sitemap.xml"));

foreach (var entry in entries)
{
    Console.WriteLine($"{entry.Url} (modified: {entry.LastModified}, priority: {entry.Priority})");
}

// Stream large sitemaps
await foreach (var entry in explorer.StreamSitemapAsync(new Uri("https://example.com/sitemap.xml")))
{
    Console.WriteLine(entry.Url);
}
```

### Filter URLs with robots.txt rules

```csharp
var robots = await explorer.GetRobotsAsync(new Uri("https://example.com"));

// Check if a path is allowed for your bot
bool allowed = robots.IsAllowed("/admin/page", userAgent: "MyBot");
```

## Providers

| Provider | Class | Auth | API Docs |
|---|---|---|---|
| DuckDuckGo | `DuckDuckGoSearchProvider` | None | [HTML Lite](https://html.duckduckgo.com/html/) |
| Google | `GoogleSearchProvider` | API Key + CX | [Custom Search JSON API](https://developers.google.com/custom-search/v1/overview) |
| Mojeek | `MojeekSearchProvider` | API Key | [Mojeek Search API](https://www.mojeek.com/services/search/web-search-api/) |
| SearchApi | `SearchApiProvider` | API Key (Bearer) | [SearchApi](https://www.searchapi.io/) |
| Tavily | `TavilySearchProvider` | API Key | [Tavily](https://tavily.com/) |

## Rate Limiting

Each provider handles rate limits automatically via a built-in `RateLimitHandler`:

1. **Detection** — Monitors HTTP 429 status and `Retry-After` headers
2. **Backoff** — Exponential backoff per provider (1s → 2s → 4s → max 30s)
3. **Fallback** — When a provider is throttled, other providers continue serving results
4. **Retry** — Up to 3 retries per request (default)

## Dependency Injection

```csharp
services.AddWebLookup(options =>
{
    options.AddDuckDuckGo();  // No API key needed
    options.AddGoogle(g =>
    {
        g.AddEngine(config["Google:ApiKey"], config["Google:Cx"]);
    });
    options.AddMojeek(config["Mojeek:ApiKey"]);
    options.AddSearchApi(config["SearchApi:ApiKey"]);
    options.AddTavily(config["Tavily:ApiKey"]);
});

// Inject wherever needed
public class MyService(WebSearchClient search, SiteExplorer explorer) { }
```

## API Reference

### SearchResult

```csharp
public record SearchResult
{
    public required string Url { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? Provider { get; init; }
}
```

### ISearchProvider

```csharp
public interface ISearchProvider
{
    string Name { get; }
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query, int count = 10,
        CancellationToken cancellationToken = default);
}
```

### RobotsInfo

```csharp
public record RobotsInfo
{
    public IReadOnlyList<RobotsRule> Rules { get; init; }
    public IReadOnlyList<string> Sitemaps { get; init; }
    public TimeSpan? CrawlDelay { get; init; }
    public bool IsAllowed(string path, string userAgent = "*");
}
```

### SitemapEntry

```csharp
public record SitemapEntry
{
    public required string Url { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public string? ChangeFrequency { get; init; }
    public double? Priority { get; init; }
}
```

## Requirements

- .NET 10.0+
- `Microsoft.Extensions.DependencyInjection.Abstractions` (for DI integration only)

## License

MIT
