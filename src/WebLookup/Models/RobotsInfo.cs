using RobotsMatching;

namespace WebLookup;

/// <summary>
/// What a site's robots.txt says (RFC 9309): the rules per user agent, the sitemaps it names, and its crawl delay.
/// </summary>
public record RobotsInfo
{
    /// <summary>
    /// The Allow and Disallow rules, in file order, with the user-agent group each belongs to.
    /// </summary>
    public IReadOnlyList<RobotsRule> Rules { get; init; } = [];
    /// <summary>
    /// The user agents the file names in <c>User-agent</c> lines, including those whose group holds no rule. A group with
    /// no rule still applies to its agents (RFC 9309 section 2.2.1), so such an agent is not sent to the <c>*</c> group.
    /// </summary>
    public IReadOnlyList<string> UserAgents { get; init; } = [];
    /// <summary>
    /// The sitemap addresses named by <c>Sitemap:</c> lines.
    /// </summary>
    public IReadOnlyList<string> Sitemaps { get; init; } = [];
    /// <summary>
    /// The last <c>Crawl-delay</c> line in the file, whichever group it sits in; <see langword="null"/> when there is none.
    /// </summary>
    public TimeSpan? CrawlDelay { get; init; }

    /// <summary>
    /// Whether <paramref name="userAgent"/> may fetch <paramref name="path"/>. A path is matched as the URL's path (and
    /// query), which always starts with <c>/</c>: <c>private</c> is read as <c>/private</c>. Percent-encoding is normalised
    /// on both sides as RFC 9309 section 2.2.2 asks: <c>/foo/%62%61%7A</c> and <c>/foo/baz</c> are the same path, and a
    /// non-ASCII path matches its escaped form.
    /// </summary>
    public bool IsAllowed(string path, string userAgent = "*")
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(userAgent);

        if (path.Length == 0 || path[0] != '/')
            path = "/" + path;

        path = RobotsPatternMatcher.Normalize(path);

        var bestAllow = -1;
        var bestDisallow = -1;

        foreach (var rule in GetMatchingRules(userAgent))
        {
            var specificity = RobotsPatternMatcher.Match(rule.Path, path);

            if (rule.Type == RobotsRuleType.Allow)
                bestAllow = Math.Max(bestAllow, specificity);
            else
                bestDisallow = Math.Max(bestDisallow, specificity);
        }

        if (bestDisallow < 0)
            return true;

        // RFC 9309: of equal specificity, Allow wins.
        return bestAllow >= bestDisallow;
    }

    private List<RobotsRule> GetMatchingRules(string userAgent)
    {
        var specific = Rules
            .Where(r => r.UserAgent.Equals(userAgent, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // A group the file names is the group for that agent even when it holds no rule: only an agent the file never names
        // falls back to the * group.
        if (specific.Count > 0 || UserAgents.Any(name => name.Equals(userAgent, StringComparison.OrdinalIgnoreCase)))
            return specific;

        return Rules
            .Where(r => r.UserAgent == "*")
            .ToList();
    }
}
