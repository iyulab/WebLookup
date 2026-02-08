using WebLookup.Site;

namespace WebLookup.Tests.Site;

public class RobotsParserTests
{
    [Fact]
    public void Parse_BasicRules_ReturnsCorrectRules()
    {
        var content = """
            User-agent: *
            Disallow: /admin/
            Allow: /admin/public/
            Crawl-delay: 10
            Sitemap: https://example.com/sitemap.xml
            """;

        var result = RobotsParser.Parse(content);

        Assert.Equal(2, result.Rules.Count);
        Assert.Single(result.Sitemaps);
        Assert.Equal(TimeSpan.FromSeconds(10), result.CrawlDelay);
        Assert.Equal("https://example.com/sitemap.xml", result.Sitemaps[0]);
    }

    [Fact]
    public void Parse_MultipleUserAgents_ParsesCorrectly()
    {
        var content = """
            User-agent: Googlebot
            Allow: /

            User-agent: BadBot
            Disallow: /
            """;

        var result = RobotsParser.Parse(content);

        Assert.Equal(2, result.Rules.Count);
        Assert.Equal("Googlebot", result.Rules[0].UserAgent);
        Assert.Equal(RobotsRuleType.Allow, result.Rules[0].Type);
        Assert.Equal("BadBot", result.Rules[1].UserAgent);
        Assert.Equal(RobotsRuleType.Disallow, result.Rules[1].Type);
    }

    [Fact]
    public void Parse_EmptyDisallow_IsIgnored()
    {
        var content = """
            User-agent: *
            Disallow:
            """;

        var result = RobotsParser.Parse(content);

        Assert.Empty(result.Rules);
    }

    [Fact]
    public void Parse_InlineComments_AreStripped()
    {
        var content = """
            User-agent: * # all bots
            Disallow: /private/ # secret area
            """;

        var result = RobotsParser.Parse(content);

        Assert.Single(result.Rules);
        Assert.Equal("/private/", result.Rules[0].Path);
    }

    [Fact]
    public void Parse_MultipleSitemaps_AllCollected()
    {
        var content = """
            Sitemap: https://example.com/sitemap1.xml
            Sitemap: https://example.com/sitemap2.xml
            Sitemap: https://example.com/sitemap3.xml
            """;

        var result = RobotsParser.Parse(content);

        Assert.Equal(3, result.Sitemaps.Count);
    }

    [Fact]
    public void IsAllowed_LongestPrefixMatch_AllowWins()
    {
        var info = new RobotsInfo
        {
            Rules =
            [
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/admin/" },
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Allow, Path = "/admin/public/" }
            ]
        };

        Assert.False(info.IsAllowed("/admin/secret"));
        Assert.True(info.IsAllowed("/admin/public/page"));
        Assert.True(info.IsAllowed("/public/page"));
    }

    [Fact]
    public void IsAllowed_SpecificUserAgent_TakesPriority()
    {
        var info = new RobotsInfo
        {
            Rules =
            [
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/" },
                new RobotsRule { UserAgent = "MyBot", Type = RobotsRuleType.Allow, Path = "/" }
            ]
        };

        Assert.False(info.IsAllowed("/anything", userAgent: "OtherBot"));
        Assert.True(info.IsAllowed("/anything", userAgent: "MyBot"));
    }

    [Fact]
    public void IsAllowed_NoRules_DefaultsToAllow()
    {
        var info = new RobotsInfo { Rules = [] };

        Assert.True(info.IsAllowed("/any/path"));
    }

    [Fact]
    public void IsAllowed_WildcardPattern_Matches()
    {
        var info = new RobotsInfo
        {
            Rules =
            [
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/*.pdf" }
            ]
        };

        Assert.False(info.IsAllowed("/docs/report.pdf"));
        Assert.True(info.IsAllowed("/docs/report.html"));
    }

    [Fact]
    public void IsAllowed_DollarSign_ExactMatch()
    {
        var info = new RobotsInfo
        {
            Rules =
            [
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/exact$" }
            ]
        };

        Assert.False(info.IsAllowed("/exact"));
        Assert.True(info.IsAllowed("/exact/more"));
    }

    [Fact]
    public void IsAllowed_WildcardWithDollarSign_CombinedPattern()
    {
        // RFC 9309: *.gif$ should match files ending in .gif
        var info = new RobotsInfo
        {
            Rules =
            [
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/*.gif$" }
            ]
        };

        Assert.False(info.IsAllowed("/images/photo.gif"));
        Assert.True(info.IsAllowed("/images/photo.gif/page"));
        Assert.True(info.IsAllowed("/images/photo.png"));
    }

    [Fact]
    public void IsAllowed_EqualLengthRules_AllowWins()
    {
        // RFC 9309: equal-length rules — Allow should win
        var info = new RobotsInfo
        {
            Rules =
            [
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/page" },
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Allow, Path = "/page" }
            ]
        };

        Assert.True(info.IsAllowed("/page"));
    }

    [Fact]
    public void IsAllowed_WildcardFirstPartMustMatchStart()
    {
        // Pattern /private/*.html should not match /public/private/file.html
        // because /private/ must match at the start
        var info = new RobotsInfo
        {
            Rules =
            [
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/private/*.html" }
            ]
        };

        Assert.False(info.IsAllowed("/private/page.html"));
        Assert.True(info.IsAllowed("/public/private/page.html"));
    }

    [Fact]
    public void IsAllowed_PathIsCaseSensitive()
    {
        // RFC 9309: path matching is case-sensitive
        var info = new RobotsInfo
        {
            Rules =
            [
                new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/Admin/" }
            ]
        };

        Assert.False(info.IsAllowed("/Admin/page"));
        Assert.True(info.IsAllowed("/admin/page"));
    }

    [Fact]
    public void IsAllowed_UserAgentIsCaseInsensitive()
    {
        // RFC 9309: user-agent matching is case-insensitive
        var info = new RobotsInfo
        {
            Rules =
            [
                new RobotsRule { UserAgent = "MyBot", Type = RobotsRuleType.Disallow, Path = "/secret" }
            ]
        };

        Assert.False(info.IsAllowed("/secret", userAgent: "mybot"));
        Assert.False(info.IsAllowed("/secret", userAgent: "MYBOT"));
    }
}
