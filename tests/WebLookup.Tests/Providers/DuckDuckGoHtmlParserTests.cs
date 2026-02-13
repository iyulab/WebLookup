namespace WebLookup.Tests.Providers;

public class DuckDuckGoHtmlParserTests
{
    [Fact]
    public void Parse_ValidHtml_ReturnsResults()
    {
        var html = """
            <div class="result results_links results_links_deep web-result">
                <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fpage1">Example Page</a>
                <a class="result__snippet">This is a description</a>
            </div>
            """;

        var results = DuckDuckGoHtmlParser.Parse(html);

        Assert.Single(results);
        Assert.Equal("https://example.com/page1", results[0].Url);
        Assert.Equal("Example Page", results[0].Title);
        Assert.Equal("This is a description", results[0].Description);
        Assert.Equal("DuckDuckGo", results[0].Provider);
    }

    [Fact]
    public void Parse_MultipleResults_ReturnsAll()
    {
        var html = """
            <div class="result results_links web-result">
                <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2F1">First</a>
                <a class="result__snippet">First desc</a>
            </div>
            <div class="result results_links web-result">
                <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2F2">Second</a>
                <a class="result__snippet">Second desc</a>
            </div>
            """;

        var results = DuckDuckGoHtmlParser.Parse(html);

        Assert.Equal(2, results.Count);
        Assert.Equal("https://example.com/1", results[0].Url);
        Assert.Equal("https://example.com/2", results[1].Url);
    }

    [Fact]
    public void Parse_EmptyHtml_ReturnsEmpty()
    {
        var results = DuckDuckGoHtmlParser.Parse("<html><body></body></html>");

        Assert.Empty(results);
    }

    [Fact]
    public void Parse_HtmlTagsInContent_StripsHtml()
    {
        var html = """
            <div class="result results_links web-result">
                <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com">The <b>Bold</b> Title</a>
                <a class="result__snippet">A <b>highlighted</b> snippet</a>
            </div>
            """;

        var results = DuckDuckGoHtmlParser.Parse(html);

        Assert.Single(results);
        Assert.Equal("The Bold Title", results[0].Title);
        Assert.Equal("A highlighted snippet", results[0].Description);
    }

    [Fact]
    public void Parse_InvalidUrl_SkipsResult()
    {
        var html = """
            <div class="result results_links web-result">
                <a class="result__a" href="not-a-valid-url">Bad Link</a>
                <a class="result__snippet">desc</a>
            </div>
            """;

        var results = DuckDuckGoHtmlParser.Parse(html);

        Assert.Empty(results);
    }

    [Fact]
    public void DecodeUrl_UddgParameter_DecodesUrl()
    {
        var raw = "//duckduckgo.com/l/?uddg=https%3A%2F%2Fwww.example.com%2Fpath%3Fq%3Dtest&rut=abc";

        var url = DuckDuckGoHtmlParser.DecodeUrl(raw);

        Assert.Equal("https://www.example.com/path?q=test", url);
    }

    [Fact]
    public void DecodeUrl_DirectHttpsUrl_ReturnsAsIs()
    {
        var url = DuckDuckGoHtmlParser.DecodeUrl("https://example.com/direct");

        Assert.Equal("https://example.com/direct", url);
    }

    [Fact]
    public void DecodeUrl_InvalidScheme_ReturnsNull()
    {
        var url = DuckDuckGoHtmlParser.DecodeUrl("ftp://example.com/file");

        Assert.Null(url);
    }

    [Fact]
    public void StripHtmlTags_RemovesTags()
    {
        var result = DuckDuckGoHtmlParser.StripHtmlTags("Hello <b>world</b> and <i>more</i>");

        Assert.Equal("Hello world and more", result);
    }

    [Fact]
    public void StripHtmlTags_NoTags_ReturnsOriginal()
    {
        var result = DuckDuckGoHtmlParser.StripHtmlTags("plain text");

        Assert.Equal("plain text", result);
    }

    [Fact]
    public void Parse_NoSnippet_DescriptionIsNull()
    {
        var html = """
            <div class="result results_links web-result">
                <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com">Title Only</a>
            </div>
            """;

        var results = DuckDuckGoHtmlParser.Parse(html);

        Assert.Single(results);
        Assert.Equal("Title Only", results[0].Title);
        Assert.Null(results[0].Description);
    }
}
