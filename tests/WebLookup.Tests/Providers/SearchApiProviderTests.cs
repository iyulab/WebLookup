namespace WebLookup.Tests.Providers;

public class SearchApiProviderTests
{
    [Fact]
    public async Task SearchAsync_ReturnsResults()
    {
        var json = """
            {
                "organic_results": [
                    {
                        "position": 1,
                        "title": "SearchApi Result",
                        "link": "https://example.com/searchapi1",
                        "snippet": "A snippet from SearchApi"
                    }
                ]
            }
            """;

        var handler = new MockHttpHandler(json);
        var client = new HttpClient(handler);
        var provider = new SearchApiProvider(
            new SearchApiOptions { ApiKey = "test-key" },
            client);

        var results = await provider.SearchAsync("test");

        Assert.Single(results);
        Assert.Equal("SearchApi Result", results[0].Title);
        Assert.Equal("https://example.com/searchapi1", results[0].Url);
        Assert.Equal("A snippet from SearchApi", results[0].Description);
        Assert.Equal("SearchApi", results[0].Provider);
    }

    [Fact]
    public async Task SearchAsync_SendsBearerToken()
    {
        string? authHeader = null;
        var handler = new MockHttpHandler(request =>
        {
            authHeader = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "organic_results": [] }""")
            });
        });

        var client = new HttpClient(handler);
        var provider = new SearchApiProvider(
            new SearchApiOptions { ApiKey = "my-secret-key" },
            client);

        await provider.SearchAsync("test");

        Assert.Equal("Bearer my-secret-key", authHeader);
    }

    [Fact]
    public async Task SearchAsync_UsesConfiguredEngine()
    {
        string? requestUrl = null;
        var handler = new MockHttpHandler(request =>
        {
            requestUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "organic_results": [] }""")
            });
        });

        var client = new HttpClient(handler);
        var provider = new SearchApiProvider(
            new SearchApiOptions { ApiKey = "key", Engine = "bing" },
            client);

        await provider.SearchAsync("test");

        Assert.Contains("engine=bing", requestUrl);
    }

    [Fact]
    public async Task SearchAsync_EmptyResults_ReturnsEmpty()
    {
        var json = """{ "organic_results": [] }""";
        var handler = new MockHttpHandler(json);
        var client = new HttpClient(handler);
        var provider = new SearchApiProvider(
            new SearchApiOptions { ApiKey = "key" },
            client);

        var results = await provider.SearchAsync("no results");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_NoOrganicResults_ReturnsEmpty()
    {
        var json = """{ "search_information": { "total_results": 0 } }""";
        var handler = new MockHttpHandler(json);
        var client = new HttpClient(handler);
        var provider = new SearchApiProvider(
            new SearchApiOptions { ApiKey = "key" },
            client);

        var results = await provider.SearchAsync("test");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_SkipsItemsWithMissingFields()
    {
        var json = """
            {
                "organic_results": [
                    { "title": "Good", "link": "https://example.com/1", "snippet": "desc" },
                    { "title": "No Link" },
                    { "link": "https://example.com/2" },
                    { "title": "Also Good", "link": "https://example.com/3", "snippet": "desc2" }
                ]
            }
            """;
        var handler = new MockHttpHandler(json);
        var client = new HttpClient(handler);
        var provider = new SearchApiProvider(
            new SearchApiOptions { ApiKey = "key" },
            client);

        var results = await provider.SearchAsync("test");

        Assert.Equal(2, results.Count);
        Assert.Equal("https://example.com/1", results[0].Url);
        Assert.Equal("https://example.com/3", results[1].Url);
    }

    [Fact]
    public async Task SearchAsync_PassesCountParameter()
    {
        string? requestUrl = null;
        var handler = new MockHttpHandler(request =>
        {
            requestUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "organic_results": [] }""")
            });
        });

        var client = new HttpClient(handler);
        var provider = new SearchApiProvider(
            new SearchApiOptions { ApiKey = "key" },
            client);

        await provider.SearchAsync("test", count: 20);

        Assert.Contains("num=20", requestUrl);
    }
}
