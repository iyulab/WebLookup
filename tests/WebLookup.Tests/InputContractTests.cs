using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using WebLookup.Site;

namespace WebLookup.Tests;

/// <summary>
/// What the library says about its inputs and about itself: it identifies itself to the sites it reads, rejects arguments no
/// provider can use before it calls one, reads a robots path and a sitemap date the same way on every machine, and keeps only
/// sitemap entries a caller can fetch.
/// </summary>
public class InputContractTests
{
    private static HttpResponseMessage Ok(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/xml") };

    // ----- identity

    [Fact]
    public async Task SiteExplorer_SendsAUserAgent_WithTheClientItCreates()
    {
        var seen = new List<string>();
        var handler = new MockHttpHandler(request =>
        {
            seen.Add(request.Headers.UserAgent.ToString());
            return Task.FromResult(Ok(request.RequestUri!.AbsolutePath == "/robots.txt" ? "User-agent: *\nDisallow:" : "<urlset/>"));
        });
        using var explorer = new SiteExplorer(handler);

        await explorer.GetRobotsAsync(new Uri("https://example.com/"), TestContext.Current.CancellationToken);
        await explorer.GetSitemapAsync(new Uri("https://example.com/sitemap.xml"), TestContext.Current.CancellationToken);

        Assert.Equal(2, seen.Count);
        Assert.All(seen, ua => Assert.StartsWith("WebLookup/", ua, StringComparison.Ordinal));
        Assert.DoesNotContain("+", seen[0], StringComparison.Ordinal); // "0.3.0", not "0.3.0+<commit>"
    }

    [Fact]
    public async Task SiteExplorer_LeavesACallersOwnClientAlone()
    {
        string? userAgent = "unset";
        var client = new HttpClient(new MockHttpHandler(request =>
        {
            userAgent = request.Headers.UserAgent.Count == 0 ? null : request.Headers.UserAgent.ToString();
            return Task.FromResult(Ok("<urlset/>"));
        }));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MyCrawler/2.0");
        using var explorer = new SiteExplorer(client);

        await explorer.GetSitemapAsync(new Uri("https://example.com/sitemap.xml"), TestContext.Current.CancellationToken);

        Assert.Equal("MyCrawler/2.0", userAgent);
    }

    // ----- arguments

    [Fact]
    public void IsAllowed_ReadsAPathWithoutALeadingSlashAsRootRelative()
    {
        var robots = RobotsParser.Parse("User-agent: *\nDisallow: /private");

        Assert.False(robots.IsAllowed("/private"));
        Assert.False(robots.IsAllowed("private"));
        Assert.False(robots.IsAllowed("private/page"));
        Assert.True(robots.IsAllowed("public"));
        Assert.True(robots.IsAllowed(""));
    }

    [Fact]
    public void IsAllowed_RejectsNull()
    {
        var robots = RobotsParser.Parse("User-agent: *\nDisallow: /private");

        Assert.Throws<ArgumentNullException>(() => robots.IsAllowed(null!));
        Assert.Throws<ArgumentNullException>(() => robots.IsAllowed("/x", null!));
    }

    [Fact]
    public void AddWebLookup_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddWebLookup(null!));
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddWebLookup(b => b.AddDuckDuckGo()));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddWebLookup(b => b.AddGoogle(null!)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_RejectsAQueryWithNoText_BeforeCallingAnyProvider(string? query)
    {
        var calls = 0;
        var provider = new DuckDuckGoSearchProvider(
            new DuckDuckGoSearchOptions(),
            new HttpClient(new MockHttpHandler(_ => { calls++; return Task.FromResult(Ok("")); })));
        using var client = new WebSearchClient(provider);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => provider.SearchAsync(query!, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.SearchAsync(query!, TestContext.Current.CancellationToken));

        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SearchAsync_RejectsACountBelowOne_BeforeCallingAnyProvider(int count)
    {
        var calls = 0;
        var provider = new DuckDuckGoSearchProvider(
            new DuckDuckGoSearchOptions(),
            new HttpClient(new MockHttpHandler(_ => { calls++; return Task.FromResult(Ok("")); })));
        using var client = new WebSearchClient(provider);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provider.SearchAsync("q", count, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.SearchAsync("q", new WebSearchOptions { MaxResultsPerProvider = count }, TestContext.Current.CancellationToken));

        Assert.Equal(0, calls);
    }

    // ----- sitemap values

    private static async Task<IReadOnlyList<SitemapEntry>> ParseAsync(string xml) =>
        await SitemapParser.ParseAsync(
            new HttpClient(new MockHttpHandler(_ => Task.FromResult(Ok(xml)))),
            new Uri("https://example.com/sitemap.xml"),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task Sitemap_LastModWithoutAnOffset_IsUtc_OnEveryMachine()
    {
        var entries = await ParseAsync("""
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>https://example.com/date</loc><lastmod>2026-11-04</lastmod></url>
              <url><loc>https://example.com/time</loc><lastmod>2026-11-04T10:30:00</lastmod></url>
              <url><loc>https://example.com/offset</loc><lastmod>2026-11-04T10:30:00+09:00</lastmod></url>
            </urlset>
            """);

        Assert.Equal(new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero), entries[0].LastModified);
        Assert.Equal(TimeSpan.Zero, entries[0].LastModified!.Value.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 10, 30, 0, TimeSpan.Zero), entries[1].LastModified);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 1, 30, 0, TimeSpan.Zero), entries[2].LastModified); // an explicit offset still wins
    }

    [Fact]
    public async Task Sitemap_LocIsTrimmed_AndOnlyAbsoluteWebAddressesAreEntries()
    {
        var entries = await ParseAsync("""
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>
                 https://example.com/padded
              </loc></url>
              <url><loc></loc></url>
              <url><loc>   </loc></url>
              <url><loc>/relative/path</loc></url>
              <url><loc>ftp://example.com/file</loc></url>
              <url><loc>https://example.com/kept</loc></url>
            </urlset>
            """);

        Assert.Equal(["https://example.com/padded", "https://example.com/kept"], entries.Select(e => e.Url));
    }

    [Fact]
    public async Task Sitemap_ChildLocInAnIndexIsTrimmed()
    {
        var handler = new MockHttpHandler(request => Task.FromResult(Ok(request.RequestUri!.AbsolutePath == "/index.xml"
            ? "<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><sitemap><loc> https://example.com/child.xml </loc></sitemap></sitemapindex>"
            : "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><url><loc>https://example.com/p</loc></url></urlset>")));

        var entries = await SitemapParser.ParseAsync(new HttpClient(handler), new Uri("https://example.com/index.xml"), TestContext.Current.CancellationToken);

        Assert.Single(entries);
    }
}
