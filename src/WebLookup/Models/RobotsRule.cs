namespace WebLookup;

public enum RobotsRuleType
{
    Allow,
    Disallow
}

public record RobotsRule
{
    public required string UserAgent { get; init; }
    public required RobotsRuleType Type { get; init; }
    public required string Path { get; init; }
}
