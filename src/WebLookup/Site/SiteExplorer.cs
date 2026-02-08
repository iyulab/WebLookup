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

        try
        {
            using var response = await client.GetAsync(robotsUri, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return RobotsParser.AllowAll;

            if (!response.IsSuccessStatusCode)
                return RobotsParser.DisallowAll;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return RobotsParser.Parse(content);
        }
        catch (HttpRequestException)
        {
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
