using System.Net;

namespace WebLookup.Tests.Providers;

public class GoogleSearchProviderTests
{
    [Fact]
    public async Task SearchAsync_SingleEngine_ReturnsResults()
    {
        var json = """
            {
                "items": [
                    {
                        "title": "Google Result 1",
                        "link": "https://example.com/1",
                        "snippet": "A snippet from Google"
                    }
                ]
            }
            """;

        var handler = new MockHttpHandler(json);
        var client = new HttpClient(handler);
        var provider = new GoogleSearchProvider(
            new GoogleSearchOptions
            {
                Engines = [new GoogleSearchEngine { ApiKey = "key1", Cx = "cx1" }]
            },
            client);

        var results = await provider.SearchAsync("test");

        Assert.Single(results);
        Assert.Equal("Google Result 1", results[0].Title);
        Assert.Equal("https://example.com/1", results[0].Url);
        Assert.Equal("A snippet from Google", results[0].Description);
        Assert.Equal("Google", results[0].Provider);
    }

    [Fact]
    public async Task SearchAsync_MultipleEngines_DeduplicatesByUrl()
    {
        var requestCount = 0;
        var handler = new MockHttpHandler(request =>
        {
            var index = Interlocked.Increment(ref requestCount);
            var json = index == 1
                ? """
                  {
                      "items": [
                          { "title": "Shared Result", "link": "https://example.com/shared", "snippet": "desc" },
                          { "title": "Engine1 Only", "link": "https://example.com/engine1", "snippet": "desc" }
                      ]
                  }
                  """
                : """
                  {
                      "items": [
                          { "title": "Shared Result Dup", "link": "https://example.com/shared", "snippet": "desc2" },
                          { "title": "Engine2 Only", "link": "https://example.com/engine2", "snippet": "desc" }
                      ]
                  }
                  """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        });

        var client = new HttpClient(handler);
        var provider = new GoogleSearchProvider(
            new GoogleSearchOptions
            {
                Engines =
                [
                    new GoogleSearchEngine { ApiKey = "key1", Cx = "cx1" },
                    new GoogleSearchEngine { ApiKey = "key2", Cx = "cx2" }
                ]
            },
            client);

        var results = await provider.SearchAsync("test");

        Assert.Equal(3, results.Count);
        Assert.Single(results, r => r.Url == "https://example.com/shared");
        Assert.Single(results, r => r.Url == "https://example.com/engine1");
        Assert.Single(results, r => r.Url == "https://example.com/engine2");
    }

    [Fact]
    public async Task SearchAsync_NoItems_ReturnsEmpty()
    {
        var json = """{ "searchInformation": { "totalResults": "0" } }""";
        var handler = new MockHttpHandler(json);
        var client = new HttpClient(handler);
        var provider = new GoogleSearchProvider(
            new GoogleSearchOptions
            {
                Engines = [new GoogleSearchEngine { ApiKey = "key1", Cx = "cx1" }]
            },
            client);

        var results = await provider.SearchAsync("no results");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_SkipsItemsWithMissingFields()
    {
        var json = """
            {
                "items": [
                    { "title": "Good", "link": "https://example.com/1", "snippet": "desc" },
                    { "title": "No Link" },
                    { "link": "https://example.com/2" },
                    { "title": "Also Good", "link": "https://example.com/3", "snippet": "desc2" }
                ]
            }
            """;
        var handler = new MockHttpHandler(json);
        var client = new HttpClient(handler);
        var provider = new GoogleSearchProvider(
            new GoogleSearchOptions
            {
                Engines = [new GoogleSearchEngine { ApiKey = "key1", Cx = "cx1" }]
            },
            client);

        var results = await provider.SearchAsync("test");

        Assert.Equal(2, results.Count);
        Assert.Equal("https://example.com/1", results[0].Url);
        Assert.Equal("https://example.com/3", results[1].Url);
    }

    [Fact]
    public async Task SearchAsync_NumCappedAt10()
    {
        string? requestUrl = null;
        var handler = new MockHttpHandler(request =>
        {
            requestUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "items": [] }""")
            });
        });

        var client = new HttpClient(handler);
        var provider = new GoogleSearchProvider(
            new GoogleSearchOptions
            {
                Engines = [new GoogleSearchEngine { ApiKey = "key1", Cx = "cx1" }]
            },
            client);

        await provider.SearchAsync("test", count: 50);

        Assert.Contains("num=10", requestUrl);
    }

    [Fact]
    public async Task SearchAsync_CountBelowCap_UsesOriginalCount()
    {
        string? requestUrl = null;
        var handler = new MockHttpHandler(request =>
        {
            requestUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "items": [] }""")
            });
        });

        var client = new HttpClient(handler);
        var provider = new GoogleSearchProvider(
            new GoogleSearchOptions
            {
                Engines = [new GoogleSearchEngine { ApiKey = "key1", Cx = "cx1" }]
            },
            client);

        await provider.SearchAsync("test", count: 5);

        Assert.Contains("num=5", requestUrl);
    }

    [Fact]
    public async Task SearchAsync_EngineFailure_ReturnsOtherEngineResults()
    {
        var requestCount = 0;
        var handler = new MockHttpHandler(request =>
        {
            var index = Interlocked.Increment(ref requestCount);
            if (index == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("error")
                });
            }

            var json = """
                {
                    "items": [
                        { "title": "Surviving Result", "link": "https://example.com/ok", "snippet": "desc" }
                    ]
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        });

        var client = new HttpClient(handler);
        var provider = new GoogleSearchProvider(
            new GoogleSearchOptions
            {
                Engines =
                [
                    new GoogleSearchEngine { ApiKey = "key1", Cx = "cx_fail" },
                    new GoogleSearchEngine { ApiKey = "key2", Cx = "cx_ok" }
                ]
            },
            client);

        var results = await provider.SearchAsync("test");

        Assert.Single(results);
        Assert.Equal("Surviving Result", results[0].Title);
    }
}
