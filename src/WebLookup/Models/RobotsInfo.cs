namespace WebLookup;

public record RobotsInfo
{
    public IReadOnlyList<RobotsRule> Rules { get; init; } = [];
    public IReadOnlyList<string> Sitemaps { get; init; } = [];
    public TimeSpan? CrawlDelay { get; init; }

    public bool IsAllowed(string path, string userAgent = "*")
    {
        var matchingRules = GetMatchingRules(userAgent);

        if (matchingRules.Count == 0)
            return true;

        RobotsRule? bestMatch = null;
        var bestLength = -1;

        foreach (var rule in matchingRules)
        {
            if (!PathMatches(path, rule.Path))
                continue;

            var ruleLength = rule.Path.Length;

            if (ruleLength > bestLength)
            {
                bestMatch = rule;
                bestLength = ruleLength;
            }
            else if (ruleLength == bestLength && rule.Type == RobotsRuleType.Allow)
            {
                // RFC 9309: equal-length rules — Allow wins
                bestMatch = rule;
            }
        }

        if (bestMatch is null)
            return true;

        return bestMatch.Type == RobotsRuleType.Allow;
    }

    private List<RobotsRule> GetMatchingRules(string userAgent)
    {
        var specific = Rules
            .Where(r => r.UserAgent.Equals(userAgent, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (specific.Count > 0)
            return specific;

        return Rules
            .Where(r => r.UserAgent == "*")
            .ToList();
    }

    private static bool PathMatches(string path, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return true;

        var mustMatchEnd = pattern.EndsWith('$');
        var effectivePattern = mustMatchEnd ? pattern[..^1] : pattern;

        if (effectivePattern.Contains('*'))
        {
            var parts = effectivePattern.Split('*');
            var index = 0;

            // First part must match at the start if non-empty
            if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
            {
                if (!path.StartsWith(parts[0], StringComparison.Ordinal))
                    return false;
                index = parts[0].Length;
            }

            for (var i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;

                var found = path.IndexOf(parts[i], index, StringComparison.Ordinal);
                if (found < 0)
                    return false;
                index = found + parts[i].Length;
            }

            if (mustMatchEnd)
                return index == path.Length;

            return true;
        }

        if (mustMatchEnd)
            return path.Equals(effectivePattern, StringComparison.Ordinal);

        return path.StartsWith(effectivePattern, StringComparison.Ordinal);
    }
}
