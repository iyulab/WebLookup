using System.Text.RegularExpressions;

namespace WebLookup;

internal static partial class DuckDuckGoHtmlParser
{
    public static IReadOnlyList<SearchResult> Parse(string html)
    {
        var results = new List<SearchResult>();
        var resultMatches = ResultBlockRegex().Matches(html);

        foreach (Match block in resultMatches)
        {
            var linkMatch = LinkRegex().Match(block.Value);
            if (!linkMatch.Success)
                continue;

            var rawHref = linkMatch.Groups[1].Value;
            var url = DecodeUrl(rawHref);
            if (url is null)
                continue;

            var title = StripHtmlTags(linkMatch.Groups[2].Value).Trim();
            if (string.IsNullOrEmpty(title))
                continue;

            string? description = null;
            var snippetMatch = SnippetRegex().Match(block.Value);
            if (snippetMatch.Success)
            {
                description = StripHtmlTags(snippetMatch.Groups[1].Value).Trim();
                if (string.IsNullOrEmpty(description))
                    description = null;
            }

            results.Add(new SearchResult
            {
                Url = url,
                Title = title,
                Description = description,
                Provider = "DuckDuckGo"
            });
        }

        return results;
    }

    internal static string? DecodeUrl(string rawHref)
    {
        var uddgMatch = UddgRegex().Match(rawHref);
        var urlString = uddgMatch.Success
            ? Uri.UnescapeDataString(uddgMatch.Groups[1].Value)
            : rawHref;

        if (Uri.TryCreate(urlString, UriKind.Absolute, out var uri)
            && (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            return uri.AbsoluteUri;
        }

        return null;
    }

    internal static string StripHtmlTags(string input)
    {
        return HtmlTagRegex().Replace(input, string.Empty);
    }

    [GeneratedRegex("""<div[^>]*class="[^"]*result[^"]*"[^>]*>.*?(?=<div[^>]*class="[^"]*result[^"]*"|$)""", RegexOptions.Singleline)]
    private static partial Regex ResultBlockRegex();

    [GeneratedRegex("""<a[^>]*class="[^"]*result__a[^"]*"[^>]*href="([^"]*)"[^>]*>(.*?)</a>""", RegexOptions.Singleline)]
    private static partial Regex LinkRegex();

    [GeneratedRegex("""<a[^>]*class="[^"]*result__snippet[^"]*"[^>]*>(.*?)</a>""", RegexOptions.Singleline)]
    private static partial Regex SnippetRegex();

    [GeneratedRegex("""uddg=([^&]+)""")]
    private static partial Regex UddgRegex();

    [GeneratedRegex("""<[^>]+>""")]
    private static partial Regex HtmlTagRegex();
}
