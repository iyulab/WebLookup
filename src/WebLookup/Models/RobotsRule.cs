namespace WebLookup;

/// <summary>
/// Whether a robots.txt rule allows or disallows the paths it matches.
/// </summary>
public enum RobotsRuleType
{
    /// <summary>
    /// The path may be fetched.
    /// </summary>
    Allow,
    /// <summary>
    /// The path may not be fetched.
    /// </summary>
    Disallow
}

/// <summary>
/// One Allow or Disallow line of a robots.txt, with the user-agent group it was written under.
/// </summary>
public record RobotsRule
{
    /// <summary>
    /// The group's user-agent token, or <c>"*"</c>.
    /// </summary>
    public required string UserAgent { get; init; }
    /// <summary>
    /// Whether the rule allows or disallows.
    /// </summary>
    public required RobotsRuleType Type { get; init; }
    /// <summary>
    /// The path pattern; <c>*</c> matches any run of characters and a trailing <c>$</c> anchors the end.
    /// </summary>
    public required string Path { get; init; }
}
