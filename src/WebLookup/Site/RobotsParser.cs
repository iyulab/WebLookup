using System.Globalization;

namespace WebLookup.Site;

internal static class RobotsParser
{
    public static RobotsInfo Parse(string content)
    {
        var rules = new List<RobotsRule>();
        var sitemaps = new List<string>();
        TimeSpan? crawlDelay = null;
        // RFC 9309 section 2.1: consecutive user-agent lines open one group, and its rules apply to every agent named.
        var currentUserAgents = new List<string> { "*" };
        var previousWasUserAgent = false;

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
                if (!previousWasUserAgent)
                    currentUserAgents = [];
                currentUserAgents.Add(value);
                previousWasUserAgent = true;
                continue;
            }

            previousWasUserAgent = false;

            if (directive.Equals("Allow", StringComparison.OrdinalIgnoreCase))
            {
                AddRules(rules, currentUserAgents, RobotsRuleType.Allow, value);
            }
            else if (directive.Equals("Disallow", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(value))
                    AddRules(rules, currentUserAgents, RobotsRuleType.Disallow, value);
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

    private static void AddRules(List<RobotsRule> rules, List<string> userAgents, RobotsRuleType type, string path)
    {
        foreach (var userAgent in userAgents)
            rules.Add(new RobotsRule { UserAgent = userAgent, Type = type, Path = path });
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
