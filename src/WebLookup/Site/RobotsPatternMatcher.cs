// This file is copied verbatim into every package that reads robots.txt, so that each of them gives the same answer for the
// same rule and path. Change every copy together.
using System.Text;

namespace RobotsMatching;

/// <summary>
/// Compares a robots.txt rule pattern with a URL path the way RFC 9309 section 2.2.2 and 2.2.3 describe.
/// </summary>
/// <remarks>
/// <para>
/// Both sides are normalised before they are compared: a percent-encoded octet of an unreserved character is
/// decoded (<c>%62</c> is <c>b</c>), any other percent-encoded octet keeps its escape with upper-case hex digits
/// (<c>%2f</c> is <c>%2F</c>, so an escaped slash is still not a slash), and a character outside ASCII is
/// percent-encoded from its UTF-8 octets. That makes <c>/foo/%62%61%7A</c> and <c>/foo/baz</c> the same path.
/// </para>
/// <para>
/// A pattern matches from the start of the path; <c>*</c> stands for any run of characters and a trailing
/// <c>$</c> anchors the end. The match is a linear scan, not a regular expression: robots.txt is untrusted input
/// and a pattern of many <c>*</c> segments compiled to a regex is a catastrophic-backtracking hazard.
/// </para>
/// </remarks>
internal static class RobotsPatternMatcher
{
    private const string HexDigits = "0123456789ABCDEF";

    /// <summary>The normal form of a pattern or path (see the type remarks); the result is ASCII.</summary>
    public static string Normalize(string text)
    {
        if (IsNormal(text))
            return text;

        var bytes = Encoding.UTF8.GetBytes(text);
        var result = new StringBuilder(bytes.Length + 8);
        for (var i = 0; i < bytes.Length; i++)
        {
            var current = bytes[i];
            if (current == (byte)'%' && i + 2 < bytes.Length && IsHex(bytes[i + 1]) && IsHex(bytes[i + 2]))
            {
                var value = (HexValue(bytes[i + 1]) << 4) | HexValue(bytes[i + 2]);
                if (IsUnreserved(value))
                    result.Append((char)value);
                else
                    AppendEscape(result, value);
                i += 2;
            }
            else if (current >= 0x80)
            {
                AppendEscape(result, current);
            }
            else
            {
                result.Append((char)current);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Matches one rule pattern against a path that went through <see cref="Normalize"/>.
    /// </summary>
    /// <returns>
    /// The specificity of the rule (the octet length of its normalised pattern, which RFC 9309 uses to pick the most
    /// specific rule), or -1 when the pattern does not match.
    /// </returns>
    public static int Match(string pattern, string normalizedPath)
    {
        pattern = Normalize(pattern);

        var anchored = pattern.EndsWith('$');
        var body = anchored ? pattern[..^1] : pattern;

        return IsMatch(body, anchored, normalizedPath) ? pattern.Length : -1;
    }

    private static bool IsMatch(string pattern, bool anchored, string path)
    {
        if (pattern.Length == 0)
            return !anchored || path.Length == 0;

        var segments = pattern.Split('*');

        // The first segment is anchored at the start of the path.
        var first = segments[0];
        if (!path.StartsWith(first, StringComparison.Ordinal))
            return false;
        var position = first.Length;

        for (var i = 1; i < segments.Length; i++)
        {
            var segment = segments[i];
            var isLast = i == segments.Length - 1;

            if (segment.Length == 0)
            {
                // A '*' with nothing after it consumes the remainder, which also satisfies '$'.
                if (isLast)
                    return true;
                continue;
            }

            if (isLast && anchored)
            {
                // The final segment must sit at the very end, and after everything matched so far.
                return path.Length - segment.Length >= position &&
                       path.EndsWith(segment, StringComparison.Ordinal);
            }

            var index = path.IndexOf(segment, position, StringComparison.Ordinal);
            if (index < 0)
                return false;
            position = index + segment.Length;
        }

        return !anchored || position == path.Length;
    }

    private static bool IsNormal(string text)
    {
        foreach (var character in text)
        {
            if (character is '%' or >= (char)0x80)
                return false;
        }

        return true;
    }

    private static void AppendEscape(StringBuilder builder, int value)
        => builder.Append('%').Append(HexDigits[value >> 4]).Append(HexDigits[value & 0xF]);

    private static bool IsHex(byte value)
        => value is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'F' or >= (byte)'a' and <= (byte)'f';

    private static int HexValue(byte value)
        => value <= (byte)'9' ? value - '0' : (value | 0x20) - 'a' + 10;

    private static bool IsUnreserved(int value)
        => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '.' or '_' or '~';
}
