namespace WebLookup.Tests.Models;

public class RobotsInfoTests
{
    [Fact]
    public void Defaults_EmptyRulesAndSitemaps()
    {
        var info = new RobotsInfo();

        Assert.Empty(info.Rules);
        Assert.Empty(info.Sitemaps);
        Assert.Null(info.CrawlDelay);
    }

    [Fact]
    public void WithInit_SetsAllProperties()
    {
        var rules = new List<RobotsRule>
        {
            new() { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/admin/" }
        };
        var sitemaps = new List<string> { "https://example.com/sitemap.xml" };

        var info = new RobotsInfo
        {
            Rules = rules,
            Sitemaps = sitemaps,
            CrawlDelay = TimeSpan.FromSeconds(10)
        };

        Assert.Single(info.Rules);
        Assert.Single(info.Sitemaps);
        Assert.Equal(TimeSpan.FromSeconds(10), info.CrawlDelay);
    }

    [Fact]
    public void IsAllowed_EmptyPattern_MatchesAll()
    {
        var info = new RobotsInfo
        {
            Rules =
            [
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "" }
            ]
        };

        // Empty path pattern should match everything (as per PathMatches)
        Assert.False(info.IsAllowed("/any/path"));
    }

    [Fact]
    public void IsAllowed_NoMatchingRulesForPath_AllowsByDefault()
    {
        var info = new RobotsInfo
        {
            Rules =
            [
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/blocked/" }
            ]
        };

        Assert.True(info.IsAllowed("/allowed/page"));
    }

    [Fact]
    public void IsAllowed_MultipleWildcards_MatchesCorrectly()
    {
        var info = new RobotsInfo
        {
            Rules =
            [
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/dir1/*/dir2/*.html" }
            ]
        };

        Assert.False(info.IsAllowed("/dir1/foo/dir2/page.html"));
        Assert.False(info.IsAllowed("/dir1/bar/baz/dir2/index.html"));
        Assert.True(info.IsAllowed("/dir1/foo/dir2/page.json"));
    }

    [Fact]
    public void IsAllowed_WildcardWithDollarAtEnd_StrictEnding()
    {
        var info = new RobotsInfo
        {
            Rules =
            [
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/page*end$" }
            ]
        };

        Assert.False(info.IsAllowed("/page-something-end"));
        Assert.True(info.IsAllowed("/page-something-end/more"));
    }

    [Fact]
    public void IsAllowed_FallbackToWildcardAgent_WhenNoSpecificMatch()
    {
        var info = new RobotsInfo
        {
            Rules =
            [
                new RobotsRule { UserAgent = "SpecificBot", Type = RobotsRuleType.Allow, Path = "/" },
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/private/" }
            ]
        };

        // UnknownBot doesn't match "SpecificBot", falls back to "*"
        Assert.False(info.IsAllowed("/private/secret", userAgent: "UnknownBot"));
        // SpecificBot has its own rules, doesn't use "*"
        Assert.True(info.IsAllowed("/private/secret", userAgent: "SpecificBot"));
    }
}

public class RobotsRuleTests
{
    [Fact]
    public void AllProperties_SetCorrectly()
    {
        var rule = new RobotsRule
        {
            UserAgent = "Googlebot",
            Type = RobotsRuleType.Allow,
            Path = "/public/"
        };

        Assert.Equal("Googlebot", rule.UserAgent);
        Assert.Equal(RobotsRuleType.Allow, rule.Type);
        Assert.Equal("/public/", rule.Path);
    }

    [Fact]
    public void RobotsRuleType_HasExpectedValues()
    {
        Assert.Equal(0, (int)RobotsRuleType.Allow);
        Assert.Equal(1, (int)RobotsRuleType.Disallow);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var rule1 = new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/admin/" };
        var rule2 = new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/admin/" };

        Assert.Equal(rule1, rule2);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var rule1 = new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/admin/" };
        var rule2 = new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Allow, Path = "/admin/" };

        Assert.NotEqual(rule1, rule2);
    }
}

public class SearchResultTests
{
    [Fact]
    public void AllProperties_SetCorrectly()
    {
        var result = new SearchResult
        {
            Url = "https://example.com",
            Title = "Example",
            Description = "An example site",
            Provider = "Google"
        };

        Assert.Equal("https://example.com", result.Url);
        Assert.Equal("Example", result.Title);
        Assert.Equal("An example site", result.Description);
        Assert.Equal("Google", result.Provider);
    }

    [Fact]
    public void OptionalProperties_DefaultToNull()
    {
        var result = new SearchResult
        {
            Url = "https://example.com",
            Title = "Example"
        };

        Assert.Null(result.Description);
        Assert.Null(result.Provider);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var r1 = new SearchResult { Url = "https://a.com", Title = "A" };
        var r2 = new SearchResult { Url = "https://a.com", Title = "A" };

        Assert.Equal(r1, r2);
    }
}

public class SitemapEntryTests
{
    [Fact]
    public void AllProperties_SetCorrectly()
    {
        var entry = new SitemapEntry
        {
            Url = "https://example.com/page",
            LastModified = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            ChangeFrequency = "weekly",
            Priority = 0.8
        };

        Assert.Equal("https://example.com/page", entry.Url);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero), entry.LastModified);
        Assert.Equal("weekly", entry.ChangeFrequency);
        Assert.Equal(0.8, entry.Priority);
    }

    [Fact]
    public void OptionalProperties_DefaultToNull()
    {
        var entry = new SitemapEntry { Url = "https://example.com" };

        Assert.Null(entry.LastModified);
        Assert.Null(entry.ChangeFrequency);
        Assert.Null(entry.Priority);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var e1 = new SitemapEntry { Url = "https://a.com" };
        var e2 = new SitemapEntry { Url = "https://a.com" };

        Assert.Equal(e1, e2);
    }
}

public class WebSearchOptionsTests
{
    [Fact]
    public void Default_MaxResultsPerProvider_Is10()
    {
        var options = new WebSearchOptions();

        Assert.Equal(10, options.MaxResultsPerProvider);
    }

    [Fact]
    public void MaxResultsPerProvider_IsSettable()
    {
        var options = new WebSearchOptions { MaxResultsPerProvider = 25 };

        Assert.Equal(25, options.MaxResultsPerProvider);
    }
}

public class ProviderOptionsTests
{
    [Fact]
    public void MojeekSearchOptions_RequiresApiKey()
    {
        var options = new MojeekSearchOptions { ApiKey = "test-key" };

        Assert.Equal("test-key", options.ApiKey);
    }

    [Fact]
    public void SearchApiOptions_DefaultEngine_IsGoogle()
    {
        var options = new SearchApiOptions { ApiKey = "test-key" };

        Assert.Equal("google", options.Engine);
    }

    [Fact]
    public void SearchApiOptions_Engine_IsSettable()
    {
        var options = new SearchApiOptions { ApiKey = "key", Engine = "bing" };

        Assert.Equal("bing", options.Engine);
    }

    [Fact]
    public void TavilySearchOptions_RequiresApiKey()
    {
        var options = new TavilySearchOptions { ApiKey = "tavily-key" };

        Assert.Equal("tavily-key", options.ApiKey);
    }

    [Fact]
    public void GoogleSearchOptions_DefaultEngines_IsEmpty()
    {
        var options = new GoogleSearchOptions();

        Assert.Empty(options.Engines);
    }

    [Fact]
    public void GoogleSearchEngine_AllProperties()
    {
        var engine = new GoogleSearchEngine { ApiKey = "api-key", Cx = "search-engine-id" };

        Assert.Equal("api-key", engine.ApiKey);
        Assert.Equal("search-engine-id", engine.Cx);
    }

    [Fact]
    public void DuckDuckGoSearchOptions_DefaultRegion_IsNull()
    {
        var options = new DuckDuckGoSearchOptions();

        Assert.Null(options.Region);
    }

    [Fact]
    public void DuckDuckGoSearchOptions_Region_IsSettable()
    {
        var options = new DuckDuckGoSearchOptions { Region = "us-en" };

        Assert.Equal("us-en", options.Region);
    }
}
