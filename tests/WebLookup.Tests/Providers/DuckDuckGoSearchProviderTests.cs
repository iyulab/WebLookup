namespace WebLookup.Tests.Providers;

public class DuckDuckGoSearchProviderTests
{
    private static string MakeHtml(params (string url, string title, string desc)[] items)
    {
        var blocks = items.Select(i => $"""
            <div class="result results_links web-result">
                <a class="result__a" href="//duckduckgo.com/l/?uddg={Uri.EscapeDataString(i.url)}">{i.title}</a>
                <a class="result__snippet">{i.desc}</a>
            </div>
            """);
        return string.Join("\n", blocks);
    }

    [Fact]
    public async Task SearchAsync_ReturnsResults()
    {
        var html = MakeHtml(
            ("https://example.com/1", "Result One", "First description"),
            ("https://example.com/2", "Result Two", "Second description"));

        var handler = new MockHttpHandler(html);
        var client = new HttpClient(handler);
        var provider = new DuckDuckGoSearchProvider(new DuckDuckGoSearchOptions(), client);

        var results = await provider.SearchAsync("test");

        Assert.Equal(2, results.Count);
        Assert.Equal("Result One", results[0].Title);
        Assert.Equal("https://example.com/1", results[0].Url);
        Assert.Equal("First description", results[0].Description);
        Assert.Equal("DuckDuckGo", results[0].Provider);
    }

    [Fact]
    public async Task SearchAsync_UsesPostMethod()
    {
        HttpMethod? method = null;
        var handler = new MockHttpHandler(request =>
        {
            method = request.Method;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>")
            });
        });

        var client = new HttpClient(handler);
        var provider = new DuckDuckGoSearchProvider(new DuckDuckGoSearchOptions(), client);

        await provider.SearchAsync("test");

        Assert.Equal(HttpMethod.Post, method);
    }

    [Fact]
    public async Task SearchAsync_LimitsResultsByCount()
    {
        var html = MakeHtml(
            ("https://example.com/1", "R1", "d1"),
            ("https://example.com/2", "R2", "d2"),
            ("https://example.com/3", "R3", "d3"),
            ("https://example.com/4", "R4", "d4"),
            ("https://example.com/5", "R5", "d5"));

        var handler = new MockHttpHandler(html);
        var client = new HttpClient(handler);
        var provider = new DuckDuckGoSearchProvider(new DuckDuckGoSearchOptions(), client);

        var results = await provider.SearchAsync("test", count: 3);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task SearchAsync_DecodesUddgUrl()
    {
        var html = """
            <div class="result results_links web-result">
                <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fpath%3Fq%3Dhello%26lang%3Den">Title</a>
                <a class="result__snippet">desc</a>
            </div>
            """;

        var handler = new MockHttpHandler(html);
        var client = new HttpClient(handler);
        var provider = new DuckDuckGoSearchProvider(new DuckDuckGoSearchOptions(), client);

        var results = await provider.SearchAsync("test");

        Assert.Single(results);
        Assert.Equal("https://example.com/path?q=hello&lang=en", results[0].Url);
    }

    [Fact]
    public async Task SearchAsync_StripsHtmlTags()
    {
        var html = """
            <div class="result results_links web-result">
                <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com">The <b>Bold</b> Title</a>
                <a class="result__snippet">A <b>highlighted</b> snippet</a>
            </div>
            """;

        var handler = new MockHttpHandler(html);
        var client = new HttpClient(handler);
        var provider = new DuckDuckGoSearchProvider(new DuckDuckGoSearchOptions(), client);

        var results = await provider.SearchAsync("test");

        Assert.Single(results);
        Assert.Equal("The Bold Title", results[0].Title);
        Assert.Equal("A highlighted snippet", results[0].Description);
    }

    [Fact]
    public async Task SearchAsync_PassesRegionParameter()
    {
        string? body = null;
        var handler = new MockHttpHandler(async request =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>")
            };
        });

        var client = new HttpClient(handler);
        var provider = new DuckDuckGoSearchProvider(
            new DuckDuckGoSearchOptions { Region = "kr-ko" }, client);

        await provider.SearchAsync("test");

        Assert.Contains("kl=kr-ko", body);
    }

    [Fact]
    public async Task SearchAsync_NoRegion_OmitsKlParameter()
    {
        string? body = null;
        var handler = new MockHttpHandler(async request =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>")
            };
        });

        var client = new HttpClient(handler);
        var provider = new DuckDuckGoSearchProvider(new DuckDuckGoSearchOptions(), client);

        await provider.SearchAsync("test");

        Assert.DoesNotContain("kl=", body);
    }

    [Fact]
    public async Task SearchAsync_EmptyHtml_ReturnsEmpty()
    {
        var handler = new MockHttpHandler("<html><body></body></html>");
        var client = new HttpClient(handler);
        var provider = new DuckDuckGoSearchProvider(new DuckDuckGoSearchOptions(), client);

        var results = await provider.SearchAsync("no results");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_SkipsItemsWithMissingFields()
    {
        var html = """
            <div class="result results_links web-result">
                <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2F1">Good Result</a>
                <a class="result__snippet">desc</a>
            </div>
            <div class="result results_links web-result">
                <a class="result__a" href="not-a-valid-url">Bad URL</a>
                <a class="result__snippet">desc</a>
            </div>
            <div class="result results_links web-result">
                <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2F3">Also Good</a>
                <a class="result__snippet">desc3</a>
            </div>
            """;

        var handler = new MockHttpHandler(html);
        var client = new HttpClient(handler);
        var provider = new DuckDuckGoSearchProvider(new DuckDuckGoSearchOptions(), client);

        var results = await provider.SearchAsync("test");

        Assert.Equal(2, results.Count);
        Assert.Equal("https://example.com/1", results[0].Url);
        Assert.Equal("https://example.com/3", results[1].Url);
    }

    [Fact]
    public void Name_ReturnsDuckDuckGo()
    {
        var provider = new DuckDuckGoSearchProvider();

        Assert.Equal("DuckDuckGo", provider.Name);
    }

    [Fact]
    public async Task SearchAsync_SetsRefererHeader()
    {
        string? referer = null;
        var handler = new MockHttpHandler(request =>
        {
            referer = request.Headers.Referrer?.ToString();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>")
            });
        });

        var client = new HttpClient(handler);
        var provider = new DuckDuckGoSearchProvider(new DuckDuckGoSearchOptions(), client);

        await provider.SearchAsync("test");

        Assert.Equal("https://html.duckduckgo.com/", referer);
    }
}
