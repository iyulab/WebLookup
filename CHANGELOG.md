# Changelog

All notable changes to this project are documented in this file. Versions follow
[Semantic Versioning](https://semver.org/); while the major version is 0, a minor release may contain
breaking changes, and each one is marked **Breaking** with a migration note.

## [Unreleased]

### Fixed
- **`SiteExplorer` identifies itself.** The client it creates sent no `User-Agent` on robots.txt and sitemap requests; it now sends `WebLookup/<version>`. A client you pass to `new SiteExplorer(httpClient)` is used as it is, so that constructor is the way to send your own crawler's name (the README now says so).
- **Sitemap dates and addresses read the same on every machine.** A `lastmod` without a UTC offset (`2026-11-04`) was read in the machine's time zone (`2026-11-04T00:00:00+09:00` on a Korean machine) and is now read as UTC; an explicit offset is kept. `<loc>` is trimmed, and an entry whose `<loc>` is empty, relative or not `http`/`https` is dropped instead of being returned with an unusable `Url`. **Breaking** by observation: a caller that relied on the machine's time zone for `LastModified`, or on an empty or relative `Url` appearing among the entries, sees different values. Migration: none for sitemaps that follow the protocol.
- **Arguments no provider can use are rejected before a provider is called.** `WebSearchClient.SearchAsync` and every provider's `SearchAsync` throw `ArgumentException` for a null, empty or white-space query and `ArgumentOutOfRangeException` for a count below 1 (they used to send the request); `RobotsInfo.IsAllowed` and `AddWebLookup` throw `ArgumentNullException` for null (`AddWebLookup(null)` was a `NullReferenceException`). `IsAllowed("private")` read the path as written and allowed it against `Disallow: /private`; a path is now read as the URL's path, so a missing leading `/` is added. **Breaking** by observation for the calls above that used to go through.
- **A blocked DuckDuckGo search is an error, not "no results".** When DuckDuckGo takes a client for a bot it answers with a challenge page (HTTP 202, or a page with its "anomaly" dialog); `DuckDuckGoSearchProvider` read that as a search with no hits and returned an empty list, where a 403, 429 or 500 throws. It now throws `HttpRequestException` (with the status) for the challenge page; a normal results page with no hits still returns an empty list. Result titles, snippets and links now have their HTML entities decoded (`Tom &amp; Jerry` was returned as written).
- **`WebSearchClient` throws when every provider fails.** It returned an empty list when its only provider was refused, so a caller that shows "no results" told its user the web has nothing on the query. With several providers, results from those that answered are still returned and the failures are still not reported; if none answers it throws the provider's exception (one provider) or an `AggregateException` (several). A provider's own client timeout no longer aborts the whole search as if the caller had cancelled; only the caller's `CancellationToken` does. **Breaking** by observation: code that treated an empty list from a failing sole provider as "no results" now sees an exception. Migration: catch it where an empty list was handled, or keep a second provider.
- **A sitemap file that times out is skipped like any other that cannot be fetched.** With a client timeout (`HttpClient.Timeout`) shorter than a child sitemap's response, `GetSitemapAsync` threw `TaskCanceledException` and lost the entries of the siblings that had been read, although the README says such a file yields no entries; `StreamSitemapAsync` delivered the earlier entries and then threw. Now only the caller's own cancellation (`CancellationToken`) propagates as `OperationCanceledException`; a timeout skips that file, keeps the rest, and the call returns normally (an empty result can still mean the fetch failed, as documented). **Breaking** by observation: code that caught the timeout exception around a sitemap call no longer sees one. Migration: compare the entry count with what the site should have, or give the client a timeout long enough for its slowest sitemap.
- **A sitemap index is followed once per distinct file.** `GetSitemapAsync` / `StreamSitemapAsync` only had a depth limit, so an index that listed itself twice cost 2,047 requests (2^11 - 1) and returned nothing, two indexes listing each other were fetched five or six times each, and a child listed twice yielded its entries twice. A sitemap file is now requested and read once per call (the address compared without its fragment, with the scheme and host in lower case), so the number of requests is bounded by the number of distinct files the input names, not by its shape.
- **A call stops at 50,000 distinct sitemap files** (the sitemaps.org limit for one index) by throwing `InvalidDataException`, rather than carrying on requesting whatever a broken or hostile index names. **Breaking** by observation only for an index that names more than the protocol allows: it used to be fetched without bound, and now the call throws (entries streamed before that point were already delivered). Migration: none for an index within the protocol.
- **The package links to the repository as its project site** (`PackageProjectUrl`) — its nuget.org page had no project link.

### Changed
- **The packages carry the MIT LICENSE text** (`LICENSE` at the package root) — the license expression alone gave no notice to ship with an app.
- **The nuget.org «Release Notes» of every package link to the GitHub release of its version**, whose notes are this CHANGELOG's section for that version.

### Dependencies
- `Microsoft.Extensions.DependencyInjection(.Abstractions)` 10.0.8 -> 10.0.13.
- Re-pinned third-party packages: 2 .NET servicing pins (Microsoft.Extensions) 10.0.13 -> 10.0.14 (the 10.0.14 security servicing release).

## [0.3.0] - 2026-09-30

### Changed

- **`GetRobotsAsync` follows RFC 9309 for a robots.txt it cannot read.** A `4xx` status (`401`, `403`, `410`, …) now
  means the file is unavailable and no rules apply (everything allowed); before, only `404` did and every other `4xx`
  disallowed the whole site. A `5xx` status, `429 Too Many Requests` and a connection failure still disallow
  everything.

### Fixed

- **A robots.txt request that times out now disallows everything, like a connection failure**, instead of throwing
  `TaskCanceledException` out of `GetRobotsAsync`. Cancelling the call through its token still throws.

## [0.2.4] - 2026-09-30

### Fixed

- **Every C# example in the README now compiles against the current API, and the test suite keeps it that way.** The
  `RobotsInfo` reference no longer shows a method without a body, the dependency-injection example no longer registers
  DuckDuckGo and SearchApi twice or relies on an undeclared `config`, and the API Reference type shapes are checked
  member by member against the library.
- **The README now describes what the library actually does.** Deduplication keeps the provider passed first, not the
  fastest; rate-limit retries apply only to a provider's own `HttpClient` and `Retry-After` is not capped; robots.txt
  answers other than 404 disallow everything; sitemap failures yield no entries; `StreamSitemapAsync` loads each file in
  full; the DI abstractions package is always a dependency.
- **`robots.txt` rules now apply to every user agent of a group that names several.** In `User-agent: A` /
  `User-agent: B` / `Disallow: /x`, only `B` received the rule, so `IsAllowed("/x", "A")` returned `true`. Consecutive
  `User-agent` lines now open one group, as RFC 9309 specifies, and `Rules` holds one entry per agent named.

## [0.2.3] - 2026-09-17

This file starts at 0.2.3. Changes in earlier releases were not recorded here; the commit history is
the record for them.
