using System.Net;
using WebLookup.Site;

namespace WebLookup;

public sealed class SiteExplorer : IDisposable
{
    private HttpClient? _httpClient;
    private bool _ownsClient;

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

    public async Task<IReadOnlyList<SitemapEntry>> GetSitemapAsync(
        Uri sitemapUri,
        CancellationToken cancellationToken = default)
    {
        return await SitemapParser.ParseAsync(GetHttpClient(), sitemapUri, cancellationToken);
    }

    public IAsyncEnumerable<SitemapEntry> StreamSitemapAsync(
        Uri sitemapUri,
        CancellationToken cancellationToken = default)
    {
        return SitemapParser.StreamAsync(GetHttpClient(), sitemapUri, cancellationToken);
    }

    public void Dispose()
    {
        if (_ownsClient)
            _httpClient?.Dispose();
    }
}
