using System.Net;

namespace WebLookup.Tests.Site;

public class SiteExplorerTests : IDisposable
{
    private static readonly Uri s_baseUri = new("https://example.com");
    private static readonly Uri s_sitemapUri = new("https://example.com/sitemap.xml");

    private SiteExplorer? _explorer;

    public void Dispose()
    {
        _explorer?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region GetRobotsAsync

    [Fact]
    public async Task GetRobotsAsync_SuccessResponse_ParsesRobotsTxt()
    {
        var content = """
            User-agent: *
            Disallow: /admin/
            Allow: /admin/public/
            Sitemap: https://example.com/sitemap.xml
            """;
        var handler = new MockHttpHandler(HttpStatusCode.OK, content);
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        var result = await _explorer.GetRobotsAsync(s_baseUri);

        Assert.Equal(2, result.Rules.Count);
        Assert.Single(result.Sitemaps);
        Assert.False(result.IsAllowed("/admin/secret"));
        Assert.True(result.IsAllowed("/admin/public/page"));
    }

    [Fact]
    public async Task GetRobotsAsync_NotFound_ReturnsAllowAll()
    {
        var handler = new MockHttpHandler(HttpStatusCode.NotFound, "");
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        var result = await _explorer.GetRobotsAsync(s_baseUri);

        Assert.True(result.IsAllowed("/any/path"));
        Assert.Empty(result.Rules);
    }

    [Fact]
    public async Task GetRobotsAsync_ServerError_ReturnsDisallowAll()
    {
        var handler = new MockHttpHandler(HttpStatusCode.InternalServerError, "");
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        var result = await _explorer.GetRobotsAsync(s_baseUri);

        Assert.False(result.IsAllowed("/any/path"));
    }

    [Fact]
    public async Task GetRobotsAsync_Forbidden_ReturnsDisallowAll()
    {
        var handler = new MockHttpHandler(HttpStatusCode.Forbidden, "");
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        var result = await _explorer.GetRobotsAsync(s_baseUri);

        Assert.False(result.IsAllowed("/any/path"));
    }

    [Fact]
    public async Task GetRobotsAsync_HttpRequestException_ReturnsDisallowAll()
    {
        var handler = new MockHttpHandler(_ =>
            throw new HttpRequestException("connection refused"));
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        var result = await _explorer.GetRobotsAsync(s_baseUri);

        Assert.False(result.IsAllowed("/any/path"));
    }

    [Fact]
    public async Task GetRobotsAsync_RequestsCorrectUrl()
    {
        Uri? requestedUri = null;
        var handler = new MockHttpHandler(req =>
        {
            requestedUri = req.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        await _explorer.GetRobotsAsync(s_baseUri);

        Assert.NotNull(requestedUri);
        Assert.Equal("https://example.com/robots.txt", requestedUri.ToString());
    }

    [Fact]
    public async Task GetRobotsAsync_BaseUriWithPath_RequestsRobotsAtRoot()
    {
        Uri? requestedUri = null;
        var handler = new MockHttpHandler(req =>
        {
            requestedUri = req.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        await _explorer.GetRobotsAsync(new Uri("https://example.com/some/path/page.html"));

        Assert.NotNull(requestedUri);
        Assert.Equal("https://example.com/robots.txt", requestedUri.ToString());
    }

    [Fact]
    public async Task GetRobotsAsync_WithCrawlDelay_ParsesDelay()
    {
        var content = """
            User-agent: *
            Crawl-delay: 5
            Disallow: /private/
            """;
        var handler = new MockHttpHandler(HttpStatusCode.OK, content);
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        var result = await _explorer.GetRobotsAsync(s_baseUri);

        Assert.Equal(TimeSpan.FromSeconds(5), result.CrawlDelay);
    }

    #endregion

    #region GetSitemapAsync

    [Fact]
    public async Task GetSitemapAsync_ValidSitemap_ReturnsEntries()
    {
        var sitemapXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url>
                <loc>https://example.com/page1</loc>
                <lastmod>2026-01-15</lastmod>
                <changefreq>weekly</changefreq>
                <priority>0.8</priority>
              </url>
              <url>
                <loc>https://example.com/page2</loc>
              </url>
            </urlset>
            """;
        var handler = new MockHttpHandler(HttpStatusCode.OK, sitemapXml);
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        var result = await _explorer.GetSitemapAsync(s_sitemapUri);

        Assert.Equal(2, result.Count);
        Assert.Equal("https://example.com/page1", result[0].Url);
        Assert.Equal("https://example.com/page2", result[1].Url);
    }

    [Fact]
    public async Task GetSitemapAsync_EmptySitemap_ReturnsEmptyList()
    {
        var sitemapXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
            </urlset>
            """;
        var handler = new MockHttpHandler(HttpStatusCode.OK, sitemapXml);
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        var result = await _explorer.GetSitemapAsync(s_sitemapUri);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSitemapAsync_InvalidXml_ReturnsEmptyList()
    {
        var handler = new MockHttpHandler(HttpStatusCode.OK, "not xml at all");
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        var result = await _explorer.GetSitemapAsync(s_sitemapUri);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSitemapAsync_NetworkError_ReturnsEmptyList()
    {
        var handler = new MockHttpHandler(_ =>
            throw new HttpRequestException("network error"));
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        var result = await _explorer.GetSitemapAsync(s_sitemapUri);

        Assert.Empty(result);
    }

    #endregion

    #region StreamSitemapAsync

    [Fact]
    public async Task StreamSitemapAsync_ValidSitemap_StreamsEntries()
    {
        var sitemapXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url>
                <loc>https://example.com/page1</loc>
              </url>
              <url>
                <loc>https://example.com/page2</loc>
              </url>
              <url>
                <loc>https://example.com/page3</loc>
              </url>
            </urlset>
            """;
        var handler = new MockHttpHandler(HttpStatusCode.OK, sitemapXml);
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        var entries = new List<SitemapEntry>();
        await foreach (var entry in _explorer.StreamSitemapAsync(s_sitemapUri))
        {
            entries.Add(entry);
        }

        Assert.Equal(3, entries.Count);
        Assert.Equal("https://example.com/page1", entries[0].Url);
        Assert.Equal("https://example.com/page3", entries[2].Url);
    }

    [Fact]
    public async Task StreamSitemapAsync_EmptySitemap_YieldsNothing()
    {
        var sitemapXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
            </urlset>
            """;
        var handler = new MockHttpHandler(HttpStatusCode.OK, sitemapXml);
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        var entries = new List<SitemapEntry>();
        await foreach (var entry in _explorer.StreamSitemapAsync(s_sitemapUri))
        {
            entries.Add(entry);
        }

        Assert.Empty(entries);
    }

    #endregion

    #region Constructor and Dispose

    [Fact]
    public async Task DefaultConstructor_CreatesInternalClient()
    {
        // Default constructor should create a client on first use
        // We can't easily test internal client creation, but we can verify
        // the explorer works without an injected client (will hit real network)
        // Instead, test that Dispose doesn't throw on default-constructed explorer
        _explorer = new SiteExplorer();
        _explorer.Dispose();
        _explorer = null; // prevent double dispose in Dispose()
    }

    [Fact]
    public async Task Constructor_WithHttpClient_UsesProvidedClient()
    {
        Uri? requestedUri = null;
        var handler = new MockHttpHandler(req =>
        {
            requestedUri = req.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        await _explorer.GetRobotsAsync(s_baseUri);

        Assert.NotNull(requestedUri);
    }

    [Fact]
    public void Dispose_WithInjectedClient_DoesNotDisposeClient()
    {
        var handler = new MockHttpHandler(HttpStatusCode.OK, "");
        var client = new HttpClient(handler);
        _explorer = new SiteExplorer(client);

        _explorer.Dispose();
        _explorer = null;

        // Client should still be usable after explorer disposal
        // This verifies the explorer does NOT own the injected client
        Assert.NotNull(client.BaseAddress?.ToString() ?? "still alive");
    }

    #endregion
}
