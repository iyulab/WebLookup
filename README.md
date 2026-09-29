# WebLookup

[![NuGet](https://img.shields.io/nuget/v/WebLookup)](https://www.nuget.org/packages/WebLookup)
[![NuGet Downloads](https://img.shields.io/nuget/dt/WebLookup)](https://www.nuget.org/packages/WebLookup)
[![Build](https://github.com/iyulab/WebLookup/actions/workflows/publish.yml/badge.svg)](https://github.com/iyulab/WebLookup/actions/workflows/publish.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A lightweight .NET library for fast URL discovery across multiple search providers with built-in rate limiting, automatic fallback, and site exploration.

**WebLookup is a URL search engine, not a content parser.** It collects URLs and metadata (title, description) from search APIs and sitemaps, then hands them off to your crawler or parser of choice.

## Features

- **Multi-provider search** — DuckDuckGo (no API key), Google, Mojeek, SearchApi, Tavily
- **Parallel execution** — `WebSearchClient` queries all of its providers at the same time
- **Fallback** — a provider that throws (network error, rejected API key, rate limit still hit after its retries) contributes no results; the other providers' results are still returned. The failure itself is not reported.
- **URL deduplication** — merges results across providers and removes duplicates
- **Site exploration** — parse `robots.txt` rules and `sitemap.xml` files, including sitemap indexes and gzip
- **Rate limit handling** — retries HTTP 429 with exponential backoff and honours `Retry-After` (when the provider creates its own `HttpClient`)
- **Small footprint** — built on `System.Net.Http`, `System.Text.Json` and `System.Xml.Linq`; the one package dependency is `Microsoft.Extensions.DependencyInjection.Abstractions`
- **DI-friendly** — `services.AddWebLookup(...)` registers `WebSearchClient` and `SiteExplorer`

## Installation

```bash
dotnet add package WebLookup
```

## Quick Start

All types are in the `WebLookup` namespace; the examples below assume `using WebLookup;`.

### Zero-config search (no API key needed)

```csharp
using WebLookup;

// DuckDuckGo requires no API key
var provider = new DuckDuckGoSearchProvider();
var results = await provider.SearchAsync("dotnet web search", count: 5);
```

### Search across multiple providers

```csharp
using var client = new WebSearchClient(
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

Providers run in parallel and `SearchAsync` returns once every provider has answered or failed. A provider that throws
contributes no results, so a rate-limited or misconfigured provider does not stop the others — and does not raise an
error either: an empty or short list can mean a provider failed. Disposing the client disposes its providers.

### Response format

`SearchAsync` returns `IReadOnlyList<SearchResult>`:

```
[DuckDuckGo] Apache Lucene.NET is a powerful open source .NET search library
  https://lucenenet.apache.org/
  Apache Lucene.Net is a .NET full-text search engine framework...

[DuckDuckGo] GitHub - apache/lucenenet: Apache Lucene.NET
  https://github.com/apache/lucenenet
  Apache Lucene.Net is a high performance search library for .NET...

[Google] WebLookup - NuGet Gallery
  https://www.nuget.org/packages/WebLookup
  A lightweight .NET library for fast URL discovery...

[Tavily] Azure Cognitive Search Documentation
  https://learn.microsoft.com/en-us/azure/search/
  Cloud search service with built-in AI capabilities...
```

Each `SearchResult` contains:

| Field | Type | Description |
|---|---|---|
| `Url` | `string` | The URL as the provider returned it |
| `Title` | `string` | Page title |
| `Description` | `string?` | Snippet or summary (may be null) |
| `Provider` | `string?` | Source provider name (`"DuckDuckGo"`, `"Google"`, `"Mojeek"`, `"SearchApi"`, `"Tavily"`) |

`WebSearchClient` **deduplicates by URL**: two URLs are the same when they differ only in letter case (anywhere in the
URL, path and query included), a `#fragment`, a default port or a trailing slash. When several providers return the same
URL, the result from the provider passed **first** to the constructor is kept — the order of the providers, not the order
in which they answer. The kept result's `Url` is not rewritten.

### Use a single provider

```csharp
// Google Custom Search
var google = new GoogleSearchProvider(new()
{
    Engines = [new() { ApiKey = "YOUR_API_KEY", Cx = "YOUR_CX" }]
});

var results = await google.SearchAsync("query", count: 5);
```

Google returns at most 10 results per engine (`count` above 10 is capped). With several `Engines`, all of them are queried
in parallel and their results merged; an engine that fails is skipped.

```csharp
// Tavily
var tavily = new TavilySearchProvider(new() { ApiKey = "YOUR_API_KEY" });

var results = await tavily.SearchAsync("query", count: 5);
```

A single provider called directly **does** throw on failure (`HttpRequestException` for a rejected key or an HTTP error);
only `WebSearchClient` turns a failure into an empty contribution. The exception is `GoogleSearchProvider`, which skips a
failing engine and so returns an empty list when its only engine fails.

### Explore a site

```csharp
using var explorer = new SiteExplorer();

// Read robots.txt (always from the site root: https://example.com/robots.txt)
var robots = await explorer.GetRobotsAsync(new Uri("https://example.com"));
Console.WriteLine($"Crawl-Delay: {robots.CrawlDelay}");
Console.WriteLine($"Sitemaps: {string.Join(", ", robots.Sitemaps)}");

foreach (var rule in robots.Rules)
{
    Console.WriteLine($"[{rule.UserAgent}] {rule.Type}: {rule.Path}");
}

// Read a sitemap (a sitemap index is followed into its child sitemaps)
var entries = await explorer.GetSitemapAsync(new Uri("https://example.com/sitemap.xml"));

foreach (var entry in entries)
{
    Console.WriteLine($"{entry.Url} (modified: {entry.LastModified}, priority: {entry.Priority})");
}

// Stream the entries instead of collecting them into a list
await foreach (var entry in explorer.StreamSitemapAsync(new Uri("https://example.com/sitemap.xml")))
{
    Console.WriteLine(entry.Url);
}
```

How `SiteExplorer` treats what the server returns:

- **robots.txt** — `404 Not Found` means no rules (everything allowed). Any other unsuccessful status (including `401`,
  `403` and `5xx`) and a connection failure (`HttpRequestException`) mean everything is disallowed.
- **Sitemaps** — a `<sitemapindex>` is followed into its child sitemaps (up to 10 levels deep); `.gz` files and
  `Content-Encoding: gzip` responses are decompressed. A sitemap that cannot be fetched or parsed yields no entries
  instead of throwing, so an empty result can mean the fetch failed. `StreamSitemapAsync` yields entries as each
  sitemap file is parsed; each file is still loaded in full, so streaming saves memory across the files of an index,
  not within one large file.
- **Crawl-Delay** — `CrawlDelay` is the last `Crawl-delay` line in the file, whichever user-agent group it sits in.

### Filter URLs with robots.txt rules

```csharp
var robots = await explorer.GetRobotsAsync(new Uri("https://example.com"));

// Check if a path is allowed for your bot
bool allowed = robots.IsAllowed("/admin/page", userAgent: "MyBot");
```

`IsAllowed` follows RFC 9309: the rules of the group naming your user agent apply (the `*` group only when no group
names it), the longest matching path wins, and `Allow` wins a tie; `*` and `$` in paths are supported. The user agent is
compared with the group's name as a whole token, ignoring case — pass `"MyBot"`, not `"MyBot/1.0"`.

## Providers

| Provider | Class | Auth | API Docs |
|---|---|---|---|
| DuckDuckGo | `DuckDuckGoSearchProvider` | None | [HTML version](https://html.duckduckgo.com/html/) |
| Google | `GoogleSearchProvider` | API Key + CX | [Custom Search JSON API](https://developers.google.com/custom-search/v1/overview) |
| Mojeek | `MojeekSearchProvider` | API Key | [Mojeek Search API](https://www.mojeek.com/services/search/web-search-api/) |
| SearchApi | `SearchApiProvider` | API Key (Bearer) | [SearchApi](https://www.searchapi.io/) |
| Tavily | `TavilySearchProvider` | API Key | [Tavily](https://tavily.com/) |

Every provider also has a constructor that takes your own `HttpClient` (for example one from `IHttpClientFactory`):

```csharp
var provider = new MojeekSearchProvider(new MojeekSearchOptions { ApiKey = "..." }, new HttpClient());
```

A provider you give an `HttpClient` uses it as is: the built-in rate limit handling below is not added, and the provider
does not dispose it.

### Provider options

**DuckDuckGo** — accepts an optional region code:

```csharp
var provider = new DuckDuckGoSearchProvider(new DuckDuckGoSearchOptions { Region = "us-en" });
```

**SearchApi** — defaults to `Engine = "google"`, override with any engine name the SearchApi service supports:

```csharp
var provider = new SearchApiProvider(new SearchApiOptions { ApiKey = "...", Engine = "bing" });
```

## Rate Limiting

A provider that creates its own `HttpClient` (every constructor without an `HttpClient` parameter) handles HTTP 429
responses itself:

1. **Detection** — an HTTP 429 response is retried; a `Retry-After` header (seconds or a date) sets the wait before the next attempt
2. **Backoff** — otherwise the wait doubles per consecutive 429 from the same host: 1s → 2s → 4s …, at most 30s
3. **Retry** — up to 3 retries per request; after that the provider sees the 429 as a failed request (`HttpRequestException`)
4. **Fallback** — in a `WebSearchClient`, a provider that still fails contributes no results and the others' results are returned

`WebSearchClient.SearchAsync` waits for every provider, so a throttled provider's retries delay the whole call. The
`Retry-After` wait is taken as the server sends it; it is not capped at 30s.

## Dependency Injection

```csharp
using Microsoft.Extensions.DependencyInjection;
using WebLookup;

services.AddWebLookup(options =>
{
    options.AddDuckDuckGo();              // No API key needed; or AddDuckDuckGo("us-en") for a region
    options.AddGoogle(g =>
    {
        g.AddEngine("YOUR_GOOGLE_API_KEY", "YOUR_CX");  // AddEngine again to query several engines
    });
    options.AddMojeek("YOUR_MOJEEK_API_KEY");
    options.AddSearchApi("YOUR_SEARCHAPI_API_KEY");     // engine "google"; or AddSearchApi(key, "bing")
    options.AddTavily("YOUR_TAVILY_API_KEY");
});

// Inject wherever needed
public class MyService(WebSearchClient search, SiteExplorer explorer) { }
```

`AddWebLookup` registers two singletons: `WebSearchClient` (with every provider added above, in that order) and
`SiteExplorer`. The providers themselves are not registered as `ISearchProvider`; each `Add…` call adds one provider, so
calling `AddDuckDuckGo()` twice queries DuckDuckGo twice. The API key arguments must be non-empty, and `AddGoogle` needs at
least one `AddEngine`; otherwise the call throws.

## API Reference

The blocks below show each type's public shape (checked against the library by the test suite).

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

### WebSearchOptions

Controls behavior of `WebSearchClient`. Set via `client.Options` or pass an instance to the `SearchAsync` overload.

```csharp
public class WebSearchOptions
{
    public int MaxResultsPerProvider { get; set; } = 10;
}
```

```csharp
var client = new WebSearchClient(provider);
client.Options.MaxResultsPerProvider = 5;

// or per-call
var results = await client.SearchAsync("query", new WebSearchOptions { MaxResultsPerProvider = 3 });
```

`MaxResultsPerProvider` is passed to each provider as its `count`, so a merged result can hold up to that many results per
provider before deduplication.

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
    public IReadOnlyList<RobotsRule> Rules { get; init; } = [];
    public IReadOnlyList<string> Sitemaps { get; init; } = [];
    public TimeSpan? CrawlDelay { get; init; }
}
```

plus `bool IsAllowed(string path, string userAgent = "*")` — see [Filter URLs with robots.txt rules](#filter-urls-with-robotstxt-rules).

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
- `Microsoft.Extensions.DependencyInjection.Abstractions` (installed with the package; used by `AddWebLookup`)

## License

MIT
