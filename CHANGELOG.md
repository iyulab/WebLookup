# Changelog

All notable changes to this project are documented in this file. Versions follow
[Semantic Versioning](https://semver.org/); while the major version is 0, a minor release may contain
breaking changes, and each one is marked **Breaking** with a migration note.

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
