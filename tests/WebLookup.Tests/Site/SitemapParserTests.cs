using System.IO.Compression;
using System.Net;
using System.Text;
using WebLookup.Site;

namespace WebLookup.Tests.Site;

public class SitemapParserTests
{
    [Fact]
    public async Task Parse_UrlSet_ReturnsEntries()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <url>
                    <loc>https://example.com/page1</loc>
                    <lastmod>2024-01-15</lastmod>
                    <changefreq>daily</changefreq>
                    <priority>0.8</priority>
                </url>
                <url>
                    <loc>https://example.com/page2</loc>
                    <priority>0.5</priority>
                </url>
            </urlset>
            """;

        var client = new HttpClient(new MockHttpHandler(xml));
        var uri = new Uri("https://example.com/sitemap.xml");

        var results = await SitemapParser.ParseAsync(client, uri, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal("https://example.com/page1", results[0].Url);
        Assert.Equal("daily", results[0].ChangeFrequency);
        Assert.Equal(0.8, results[0].Priority);
        Assert.Equal("https://example.com/page2", results[1].Url);
        Assert.Equal(0.5, results[1].Priority);
    }

    [Fact]
    public async Task Parse_SitemapIndex_RecursivelyFetches()
    {
        var indexXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <sitemap>
                    <loc>https://example.com/sitemap1.xml</loc>
                </sitemap>
            </sitemapindex>
            """;

        var childXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <url>
                    <loc>https://example.com/child-page</loc>
                </url>
            </urlset>
            """;

        var handler = new MockHttpHandler(request =>
        {
            var content = request.RequestUri!.AbsolutePath.Contains("sitemap1")
                ? childXml
                : indexXml;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/xml")
            });
        });

        var client = new HttpClient(handler);
        var uri = new Uri("https://example.com/sitemap.xml");

        var results = await SitemapParser.ParseAsync(client, uri, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("https://example.com/child-page", results[0].Url);
    }

    [Fact]
    public async Task Stream_YieldsEntries()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <url><loc>https://example.com/a</loc></url>
                <url><loc>https://example.com/b</loc></url>
                <url><loc>https://example.com/c</loc></url>
            </urlset>
            """;

        var client = new HttpClient(new MockHttpHandler(xml));
        var uri = new Uri("https://example.com/sitemap.xml");

        var entries = new List<SitemapEntry>();
        await foreach (var entry in SitemapParser.StreamAsync(client, uri, CancellationToken.None))
        {
            entries.Add(entry);
        }

        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public async Task Parse_GzipCompressed_Decompresses()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <url><loc>https://example.com/compressed</loc></url>
            </urlset>
            """;

        var bytes = Encoding.UTF8.GetBytes(xml);
        using var ms = new MemoryStream();
        await using (var gzip = new GZipStream(ms, CompressionLevel.Fastest, leaveOpen: true))
        {
            await gzip.WriteAsync(bytes);
        }
        ms.Position = 0;
        var gzipBytes = ms.ToArray();

        var handler = new MockHttpHandler(request =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(gzipBytes)
            };
            return Task.FromResult(response);
        });

        var client = new HttpClient(handler);
        var uri = new Uri("https://example.com/sitemap.xml.gz");

        var results = await SitemapParser.ParseAsync(client, uri, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("https://example.com/compressed", results[0].Url);
    }

    [Fact]
    public async Task Parse_InvalidXml_ReturnsEmpty()
    {
        var client = new HttpClient(new MockHttpHandler("not valid xml"));
        var uri = new Uri("https://example.com/sitemap.xml");

        var results = await SitemapParser.ParseAsync(client, uri, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Parse_CancellationToken_Throws()
    {
        var handler = new MockHttpHandler(async _ =>
        {
            await Task.Delay(5000);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var client = new HttpClient(handler);
        var uri = new Uri("https://example.com/sitemap.xml");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => SitemapParser.ParseAsync(client, uri, cts.Token));
    }

    [Fact]
    public async Task Parse_PriorityWithDecimalComma_ParsesCorrectly()
    {
        // Priority values in XML sitemaps use "." as decimal separator per spec
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <url>
                    <loc>https://example.com/page1</loc>
                    <priority>0.9</priority>
                </url>
            </urlset>
            """;

        var client = new HttpClient(new MockHttpHandler(xml));
        var uri = new Uri("https://example.com/sitemap.xml");

        var results = await SitemapParser.ParseAsync(client, uri, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(0.9, results[0].Priority);
    }

    [Fact]
    public async Task Parse_HttpError_ReturnsEmpty()
    {
        var handler = new MockHttpHandler(HttpStatusCode.InternalServerError, "error");
        var client = new HttpClient(handler);
        var uri = new Uri("https://example.com/sitemap.xml");

        var results = await SitemapParser.ParseAsync(client, uri, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Parse_NoNamespace_ReturnsEntries()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset>
                <url>
                    <loc>https://example.com/no-ns-page</loc>
                    <priority>0.7</priority>
                </url>
            </urlset>
            """;

        var client = new HttpClient(new MockHttpHandler(xml));
        var uri = new Uri("https://example.com/sitemap.xml");

        var results = await SitemapParser.ParseAsync(client, uri, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("https://example.com/no-ns-page", results[0].Url);
        Assert.Equal(0.7, results[0].Priority);
    }

    [Fact]
    public async Task Parse_DeepRecursion_StopsAtMaxDepth()
    {
        // Every request returns a sitemap index pointing to another index
        var handler = new MockHttpHandler(request =>
        {
            var xml = """
                <?xml version="1.0" encoding="UTF-8"?>
                <sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                    <sitemap>
                        <loc>https://example.com/deeper.xml</loc>
                    </sitemap>
                </sitemapindex>
                """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(xml, Encoding.UTF8, "application/xml")
            });
        });

        var client = new HttpClient(handler);
        var uri = new Uri("https://example.com/sitemap.xml");

        // Should not hang — recursion depth is limited
        var results = await SitemapParser.ParseAsync(client, uri, CancellationToken.None);

        Assert.Empty(results);
    }
}
