namespace WebLookup.Tests.Providers;

public class TavilySearchProviderTests
{
    [Fact]
    public async Task SearchAsync_ReturnsResults()
    {
        var json = """
            {
                "results": [
                    {
                        "title": "Tavily Result",
                        "url": "https://example.com/tavily1",
                        "content": "Tavily content snippet"
                    }
                ]
            }
            """;

        var handler = new MockHttpHandler(json);
        var client = new HttpClient(handler);
        var provider = new TavilySearchProvider(
            new TavilySearchOptions { ApiKey = "test-key" },
            client);

        var results = await provider.SearchAsync("test");

        Assert.Single(results);
        Assert.Equal("Tavily Result", results[0].Title);
        Assert.Equal("https://example.com/tavily1", results[0].Url);
        Assert.Equal("Tavily content snippet", results[0].Description);
        Assert.Equal("Tavily", results[0].Provider);
    }

    [Fact]
    public async Task SearchAsync_UsesPostMethod()
    {
        HttpMethod? capturedMethod = null;
        var handler = new MockHttpHandler(request =>
        {
            capturedMethod = request.Method;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "results": [] }""")
            });
        });

        var client = new HttpClient(handler);
        var provider = new TavilySearchProvider(
            new TavilySearchOptions { ApiKey = "key" },
            client);

        await provider.SearchAsync("test");

        Assert.Equal(HttpMethod.Post, capturedMethod);
    }

    [Fact]
    public async Task SearchAsync_EmptyResults_ReturnsEmpty()
    {
        var json = """{ "results": [] }""";

        var handler = new MockHttpHandler(json);
        var client = new HttpClient(handler);
        var provider = new TavilySearchProvider(
            new TavilySearchOptions { ApiKey = "test-key" },
            client);

        var results = await provider.SearchAsync("test");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_SendsApiKeyInBody()
    {
        string? capturedBody = null;
        var handler = new MockHttpHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "results": [] }""")
            };
        });

        var client = new HttpClient(handler);
        var provider = new TavilySearchProvider(
            new TavilySearchOptions { ApiKey = "my-key-123" },
            client);

        await provider.SearchAsync("test query", count: 5);

        Assert.NotNull(capturedBody);
        Assert.Contains("my-key-123", capturedBody);
        Assert.Contains("test query", capturedBody);
        Assert.Contains("5", capturedBody);
    }

    [Fact]
    public async Task SearchAsync_SkipsItemsWithMissingFields()
    {
        var json = """
            {
                "results": [
                    { "title": "Has Title", "url": "https://example.com/1", "content": "desc" },
                    { "title": "No URL" },
                    { "url": "https://example.com/2" },
                    { "title": "Complete", "url": "https://example.com/3", "content": "desc2" }
                ]
            }
            """;

        var handler = new MockHttpHandler(json);
        var client = new HttpClient(handler);
        var provider = new TavilySearchProvider(
            new TavilySearchOptions { ApiKey = "test-key" },
            client);

        var results = await provider.SearchAsync("test");

        Assert.Equal(2, results.Count);
        Assert.Equal("https://example.com/1", results[0].Url);
        Assert.Equal("https://example.com/3", results[1].Url);
    }
}
