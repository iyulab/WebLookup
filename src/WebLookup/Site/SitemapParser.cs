using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace WebLookup.Site;

internal static class SitemapParser
{
    private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private const int MaxRecursionDepth = 10;

    public static async Task<IReadOnlyList<SitemapEntry>> ParseAsync(
        HttpClient httpClient,
        Uri sitemapUri,
        CancellationToken cancellationToken)
    {
        var entries = new List<SitemapEntry>();

        await foreach (var entry in StreamAsync(httpClient, sitemapUri, cancellationToken))
        {
            entries.Add(entry);
        }

        return entries;
    }

    public static IAsyncEnumerable<SitemapEntry> StreamAsync(
        HttpClient httpClient,
        Uri sitemapUri,
        CancellationToken cancellationToken)
    {
        return StreamCoreAsync(httpClient, sitemapUri, depth: 0, cancellationToken);
    }

    private static async IAsyncEnumerable<SitemapEntry> StreamCoreAsync(
        HttpClient httpClient,
        Uri sitemapUri,
        int depth,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (depth > MaxRecursionDepth)
            yield break;

        XDocument doc;
        try
        {
            doc = await FetchXmlAsync(httpClient, sitemapUri, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            yield break;
        }

        var root = doc.Root;
        if (root is null)
            yield break;

        var ns = root.Name.Namespace;

        // Detect sitemapindex vs urlset
        if (root.Name.LocalName == "sitemapindex")
        {
            var childSitemapUrls = root
                .Elements(ns + "sitemap")
                .Select(e => e.Element(ns + "loc")?.Value)
                .Where(url => url is not null);

            foreach (var childUrl in childSitemapUrls)
            {
                if (!Uri.TryCreate(childUrl, UriKind.Absolute, out var childUri))
                    continue;

                await foreach (var entry in StreamCoreAsync(httpClient, childUri, depth + 1, cancellationToken))
                {
                    yield return entry;
                }
            }
        }
        else
        {
            foreach (var urlElement in root.Elements(ns + "url"))
            {
                var loc = urlElement.Element(ns + "loc")?.Value;
                if (loc is null)
                    continue;

                DateTimeOffset? lastMod = null;
                var lastModStr = urlElement.Element(ns + "lastmod")?.Value;
                if (lastModStr is not null && DateTimeOffset.TryParse(lastModStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    lastMod = parsed;

                double? priority = null;
                var priorityStr = urlElement.Element(ns + "priority")?.Value;
                if (priorityStr is not null && double.TryParse(priorityStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedPriority))
                    priority = parsedPriority;

                yield return new SitemapEntry
                {
                    Url = loc,
                    LastModified = lastMod,
                    ChangeFrequency = urlElement.Element(ns + "changefreq")?.Value,
                    Priority = priority
                };
            }
        }
    }

    private static async Task<XDocument> FetchXmlAsync(
        HttpClient httpClient,
        Uri uri,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        // Handle gzip-compressed sitemaps
        if (uri.AbsolutePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ||
            response.Content.Headers.ContentEncoding.Contains("gzip"))
        {
            await using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
            return await XDocument.LoadAsync(gzipStream, LoadOptions.None, cancellationToken);
        }

        return await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
    }
}
