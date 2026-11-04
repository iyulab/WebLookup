using System.Net;
using WebLookup.Site;

namespace WebLookup;

/// <summary>
/// Reads what a site publishes for crawlers: its robots.txt (RFC 9309) and its sitemaps, including sitemap indexes and gzip files.
/// </summary>
public sealed class SiteExplorer : IDisposable
{
    private HttpClient? _httpClient;
    private bool _ownsClient;

    /// <summary>
    /// Creates an explorer with its own HTTP client, which sends <c>User-Agent: WebLookup/&lt;version&gt;</c> and is disposed with the explorer.
    /// </summary>
    public SiteExplorer()
    {
    }

    /// <summary>Uses the caller's client as it is (its headers, timeout and handler); the explorer does not dispose it.</summary>
    public SiteExplorer(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _ownsClient = false;
    }

    internal SiteExplorer(HttpMessageHandler handler)
    {
        _httpClient = CreateClient(handler);
        _ownsClient = true;
    }

    /// <summary>The <c>User-Agent</c> the explorer sends from the client it creates itself: <c>WebLookup/&lt;version&gt;</c>.</summary>
    internal static string DefaultUserAgent { get; } = BuildUserAgent();

    private static string BuildUserAgent()
    {
        var version = typeof(SiteExplorer).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;

        // "0.3.0+<commit>" -> "0.3.0"
        var plus = version?.IndexOf('+', StringComparison.Ordinal) ?? -1;
        version = plus >= 0 ? version![..plus] : version;

        return string.IsNullOrWhiteSpace(version) ? "WebLookup" : $"WebLookup/{version}";
    }

    private static HttpClient CreateClient(HttpMessageHandler? handler = null)
    {
        var client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        return client;
    }

    private HttpClient GetHttpClient()
    {
        if (_httpClient is null)
        {
            _httpClient = CreateClient();
            _ownsClient = true;
        }
        return _httpClient;
    }

    /// <summary>
    /// Reads the robots.txt at the root of a site.
    /// </summary>
    /// <param name="baseUri">Any address on the site; the file is read from <c>/robots.txt</c> of its host.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The parsed rules. A 4xx status (other than 429) means no robots.txt, so everything is allowed; a 5xx status, 429, a connection failure or a timeout means it is unreachable, so everything is disallowed.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<RobotsInfo> GetRobotsAsync(
        Uri baseUri,
        CancellationToken cancellationToken = default)
    {
        var robotsUri = new Uri(baseUri, "/robots.txt");
        var client = GetHttpClient();

        // RFC 9309 §2.3.1: a robots.txt that is "unavailable" (a 4xx status) means no rules apply; one that is
        // "unreachable" (a 5xx status, a network error, a timeout) means the whole site is disallowed. 429 Too Many
        // Requests is the server saying "not now", not "there is no file", so it counts as unreachable.
        try
        {
            using var response = await client.GetAsync(robotsUri, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return RobotsParser.Parse(content);
            }

            var status = (int)response.StatusCode;
            return status is >= 400 and < 500 && response.StatusCode != HttpStatusCode.TooManyRequests
                ? RobotsParser.AllowAll
                : RobotsParser.DisallowAll;
        }
        catch (HttpRequestException)
        {
            return RobotsParser.DisallowAll;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The client's timeout, not the caller's cancellation: the file is unreachable.
            return RobotsParser.DisallowAll;
        }
    }

    /// <summary>
    /// Reads a sitemap, following a sitemap index into its child sitemaps (up to 10 levels). Each distinct file is requested once; a file that cannot be fetched or parsed, including one that times out, yields no entries.
    /// </summary>
    /// <param name="sitemapUri">The sitemap or sitemap index address.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>All entries, in document order.</returns>
    /// <exception cref="InvalidDataException">The index names more than 50,000 distinct sitemap files.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<IReadOnlyList<SitemapEntry>> GetSitemapAsync(
        Uri sitemapUri,
        CancellationToken cancellationToken = default)
    {
        return await SitemapParser.ParseAsync(GetHttpClient(), sitemapUri, cancellationToken);
    }

    /// <summary>
    /// Reads a sitemap like <see cref="GetSitemapAsync"/> but yields each entry as its file is parsed, so the entries of an index are not all held in memory.
    /// </summary>
    /// <param name="sitemapUri">The sitemap or sitemap index address.</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>The entries, in document order.</returns>
    /// <exception cref="InvalidDataException">The index names more than 50,000 distinct sitemap files; entries already yielded were delivered.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public IAsyncEnumerable<SitemapEntry> StreamSitemapAsync(
        Uri sitemapUri,
        CancellationToken cancellationToken = default)
    {
        return SitemapParser.StreamAsync(GetHttpClient(), sitemapUri, cancellationToken);
    }

    /// <summary>
    /// Disposes the HTTP client when the explorer created it.
    /// </summary>
    public void Dispose()
    {
        if (_ownsClient)
            _httpClient?.Dispose();
    }
}
