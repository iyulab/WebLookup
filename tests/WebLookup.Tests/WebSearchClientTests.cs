namespace WebLookup.Tests;

public class WebSearchClientTests
{
    [Fact]
    public async Task SearchAsync_DeduplicatesByUrl()
    {
        var provider1 = new FakeProvider("P1",
        [
            new SearchResult { Url = "https://example.com/page1", Title = "Page 1 from P1", Provider = "P1" },
            new SearchResult { Url = "https://example.com/page2", Title = "Page 2 from P1", Provider = "P1" }
        ]);

        var provider2 = new FakeProvider("P2",
        [
            new SearchResult { Url = "https://example.com/page1", Title = "Page 1 from P2", Provider = "P2" },
            new SearchResult { Url = "https://example.com/page3", Title = "Page 3 from P2", Provider = "P2" }
        ]);

        var client = new WebSearchClient(provider1, provider2);
        var results = await client.SearchAsync("test");

        Assert.Equal(3, results.Count);
        // First-seen wins: page1 should come from P1
        Assert.Equal("Page 1 from P1", results.First(r => r.Url == "https://example.com/page1").Title);
    }

    [Fact]
    public async Task SearchAsync_NormalizesUrls()
    {
        var provider1 = new FakeProvider("P1",
        [
            new SearchResult { Url = "https://Example.COM/page/", Title = "With trailing slash", Provider = "P1" }
        ]);

        var provider2 = new FakeProvider("P2",
        [
            new SearchResult { Url = "https://example.com/page", Title = "No trailing slash", Provider = "P2" }
        ]);

        var client = new WebSearchClient(provider1, provider2);
        var results = await client.SearchAsync("test");

        Assert.Single(results);
    }

    [Fact]
    public async Task SearchAsync_HandlesProviderFailure()
    {
        var failingProvider = new FakeProvider("Failing", null, shouldThrow: true);
        var workingProvider = new FakeProvider("Working",
        [
            new SearchResult { Url = "https://example.com/ok", Title = "OK", Provider = "Working" }
        ]);

        var client = new WebSearchClient(failingProvider, workingProvider);
        var results = await client.SearchAsync("test");

        Assert.Single(results);
        Assert.Equal("OK", results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_RemovesFragments()
    {
        var provider1 = new FakeProvider("P1",
        [
            new SearchResult { Url = "https://example.com/page#section1", Title = "With fragment", Provider = "P1" }
        ]);

        var provider2 = new FakeProvider("P2",
        [
            new SearchResult { Url = "https://example.com/page#section2", Title = "Different fragment", Provider = "P2" }
        ]);

        var client = new WebSearchClient(provider1, provider2);
        var results = await client.SearchAsync("test");

        Assert.Single(results);
    }

    [Fact]
    public void NormalizeUrl_VariousCases()
    {
        Assert.Equal("https://example.com/path", WebSearchClient.NormalizeUrl("https://Example.COM/path/"));
        Assert.Equal("https://example.com/path", WebSearchClient.NormalizeUrl("HTTPS://EXAMPLE.COM/path"));
        Assert.Equal("https://example.com/path?q=1", WebSearchClient.NormalizeUrl("https://example.com/path?q=1"));
        Assert.Equal("https://example.com", WebSearchClient.NormalizeUrl("https://example.com/"));
    }

    [Fact]
    public void NormalizeUrl_DefaultPortRemoved()
    {
        Assert.Equal("https://example.com/path", WebSearchClient.NormalizeUrl("https://example.com:443/path"));
        Assert.Equal("http://example.com/path", WebSearchClient.NormalizeUrl("http://example.com:80/path"));
    }

    [Fact]
    public void NormalizeUrl_NonDefaultPortPreserved()
    {
        Assert.Equal("https://example.com:8443/path", WebSearchClient.NormalizeUrl("https://example.com:8443/path"));
        Assert.Equal("http://example.com:8080/path", WebSearchClient.NormalizeUrl("http://example.com:8080/path"));
    }

    [Fact]
    public void NormalizeUrl_FragmentRemoved()
    {
        Assert.Equal("https://example.com/page", WebSearchClient.NormalizeUrl("https://example.com/page#section"));
        Assert.Equal("https://example.com/page?q=1", WebSearchClient.NormalizeUrl("https://example.com/page?q=1#top"));
    }

    [Fact]
    public void NormalizeUrl_InvalidUrl_ReturnsLowered()
    {
        Assert.Equal("not-a-url", WebSearchClient.NormalizeUrl("Not-A-URL"));
    }

    [Fact]
    public async Task SearchAsync_EmptyProviders_ReturnsEmpty()
    {
        var client = new WebSearchClient();
        var results = await client.SearchAsync("test");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_CancellationToken_ThrowsOperationCanceled()
    {
        var slowProvider = new FakeProvider("Slow", null, shouldCancel: true);
        var client = new WebSearchClient(slowProvider);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SearchAsync("test", cts.Token));
    }

    private sealed class FakeProvider : ISearchProvider
    {
        private readonly IReadOnlyList<SearchResult>? _results;
        private readonly bool _shouldThrow;
        private readonly bool _shouldCancel;

        public string Name { get; }

        public FakeProvider(string name, IReadOnlyList<SearchResult>? results, bool shouldThrow = false, bool shouldCancel = false)
        {
            Name = name;
            _results = results;
            _shouldThrow = shouldThrow;
            _shouldCancel = shouldCancel;
        }

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            string query, int count = 10, CancellationToken cancellationToken = default)
        {
            if (_shouldThrow)
                throw new HttpRequestException("Provider failed");

            if (_shouldCancel)
                throw new OperationCanceledException(cancellationToken);

            return Task.FromResult(_results ?? (IReadOnlyList<SearchResult>)[]);
        }
    }
}
