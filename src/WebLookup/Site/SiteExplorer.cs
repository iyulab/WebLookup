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

    public SiteExplorer(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _ownsClient = false;
    }

    private HttpClient GetHttpClient()
    {
        if (_httpClient is null)
        {
            _httpClient = new HttpClient();
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
