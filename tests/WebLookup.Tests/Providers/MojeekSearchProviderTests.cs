namespace WebLookup.Tests.Providers;

public class MojeekSearchProviderTests
{
    [Fact]
    public async Task SearchAsync_ReturnsResults()
    {
        var json = """
            {
                "response": {
                    "status": "OK",
                    "results": [
                        {
                            "url": "https://example.com/mojeek1",
                            "title": "Mojeek Result",
                            "desc": "A description from Mojeek"
                        },
                        {
                            "url": "https://example.com/mojeek2",
                            "title": "Another Mojeek Result",
                            "desc": "Another description"
                        }
                    ]
                }
            }
            """;

        var handler = new MockHttpHandler(json);
        var client = new HttpClient(handler);
        var provider = new MojeekSearchProvider(
            new MojeekSearchOptions { ApiKey = "test-key" },
            client);

        var results = await provider.SearchAsync("test query", count: 5);

        Assert.Equal(2, results.Count);
        Assert.Equal("Mojeek Result", results[0].Title);
        Assert.Equal("https://example.com/mojeek1", results[0].Url);
        Assert.Equal("A description from Mojeek", results[0].Description);
        Assert.Equal("Mojeek", results[0].Provider);
    }

    [Fact]
    public async Task SearchAsync_EmptyResults_ReturnsEmpty()
    {
        var json = """
            {
                "response": {
                    "status": "OK",
                    "results": []
                }
            }
            """;

        var handler = new MockHttpHandler(json);
        var client = new HttpClient(handler);
        var provider = new MojeekSearchProvider(
            new MojeekSearchOptions { ApiKey = "key" },
            client);

        var results = await provider.SearchAsync("no results");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_NoResponseProperty_ReturnsEmpty()
    {
        var json = """{ "error": "bad request" }""";
        var handler = new MockHttpHandler(json);
        var client = new HttpClient(handler);
        var provider = new MojeekSearchProvider(
            new MojeekSearchOptions { ApiKey = "key" },
            client);

        var results = await provider.SearchAsync("test");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_SkipsItemsWithMissingFields()
    {
        var json = """
            {
                "response": {
                    "results": [
                        { "url": "https://example.com/1", "title": "Good", "desc": "desc" },
                        { "url": "https://example.com/2" },
                        { "title": "No URL" },
                        { "url": "https://example.com/3", "title": "Also Good" }
                    ]
                }
            }
            """;

        var handler = new MockHttpHandler(json);
        var client = new HttpClient(handler);
        var provider = new MojeekSearchProvider(
            new MojeekSearchOptions { ApiKey = "key" },
            client);

        var results = await provider.SearchAsync("test");

        Assert.Equal(2, results.Count);
        Assert.Equal("https://example.com/1", results[0].Url);
        Assert.Equal("https://example.com/3", results[1].Url);
    }

    [Fact]
    public async Task SearchAsync_IncludesApiKeyAndQueryInUrl()
    {
        string? requestUrl = null;
        var handler = new MockHttpHandler(request =>
        {
            requestUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "response": { "results": [] } }""")
            });
        });

        var client = new HttpClient(handler);
        var provider = new MojeekSearchProvider(
            new MojeekSearchOptions { ApiKey = "my-key" },
            client);

        await provider.SearchAsync("test query", count: 7);

        Assert.NotNull(requestUrl);
        Assert.Contains("api_key=my-key", requestUrl);
        Assert.Contains("q=test", requestUrl!);
        Assert.Contains("t=7", requestUrl);
        Assert.Contains("fmt=json", requestUrl);
    }
}
