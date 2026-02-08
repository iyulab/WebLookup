using System.Globalization;

namespace WebLookup.Site;

internal static class RobotsParser
{
    public static RobotsInfo Parse(string content)
    {
        var rules = new List<RobotsRule>();
        var sitemaps = new List<string>();
        TimeSpan? crawlDelay = null;
        var currentUserAgent = "*";

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            var colonIndex = line.IndexOf(':');
            if (colonIndex < 0)
                continue;

            var directive = line[..colonIndex].Trim();
            var value = line[(colonIndex + 1)..].Trim();

            // Remove inline comments
            var commentIndex = value.IndexOf('#');
            if (commentIndex >= 0)
                value = value[..commentIndex].Trim();

            if (string.IsNullOrEmpty(value) && !directive.Equals("Disallow", StringComparison.OrdinalIgnoreCase))
                continue;

            if (directive.Equals("User-agent", StringComparison.OrdinalIgnoreCase))
            {
                currentUserAgent = value;
            }
            else if (directive.Equals("Allow", StringComparison.OrdinalIgnoreCase))
            {
                rules.Add(new RobotsRule
                {
                    UserAgent = currentUserAgent,
                    Type = RobotsRuleType.Allow,
                    Path = value
                });
            }
            else if (directive.Equals("Disallow", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    rules.Add(new RobotsRule
                    {
                        UserAgent = currentUserAgent,
                        Type = RobotsRuleType.Disallow,
                        Path = value
                    });
                }
            }
            else if (directive.Equals("Crawl-delay", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    crawlDelay = TimeSpan.FromSeconds(seconds);
                }
            }
            else if (directive.Equals("Sitemap", StringComparison.OrdinalIgnoreCase))
            {
                sitemaps.Add(value);
            }
        }

        return new RobotsInfo
        {
            Rules = rules,
            Sitemaps = sitemaps,
            CrawlDelay = crawlDelay
        };
    }

    public static RobotsInfo AllowAll => new()
    {
        Rules = [],
        Sitemaps = [],
        CrawlDelay = null
    };

    public static RobotsInfo DisallowAll => new()
    {
        Rules = [new RobotsRule { UserAgent = "*", Type = RobotsRuleType.Disallow, Path = "/" }],
        Sitemaps = [],
        CrawlDelay = null
    };
}
