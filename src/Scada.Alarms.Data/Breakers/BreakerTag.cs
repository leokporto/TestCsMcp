namespace Scada.Alarms.Data.Breakers;

/// <summary>
/// Identifies a circuit breaker tag as stored in dbo.Alarms:
/// Level = "/REGIAO_01/SE_01", TagName = "BAY_03.STA.DJ".
/// </summary>
public sealed record BreakerTag(string Level, string TagName)
{
    public const string BreakerSuffix = ".DJ";
    private const string RuntimePrefix = "Tag.";

    /// <summary>Path without leading slash, e.g. "REGIAO_01/SE_01/BAY_03.STA.DJ".</summary>
    public string FullPath => $"{Level.TrimStart('/')}/{TagName}";

    /// <summary>
    /// Parses "Tag.REGIAO_01/SE_01/BAY_03.STA.DJ", "REGIAO_01/SE_01/BAY_03.STA.DJ"
    /// or "/REGIAO_01/SE_01/BAY_03.STA.DJ".
    /// </summary>
    public static BreakerTag Parse(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new ArgumentException("Tag path is empty.");

        var path = fullPath.Trim();
        if (path.StartsWith(RuntimePrefix, StringComparison.OrdinalIgnoreCase))
            path = path[RuntimePrefix.Length..];

        var lastSlash = path.LastIndexOf('/');
        if (lastSlash <= 0 || lastSlash == path.Length - 1)
            throw new ArgumentException(
                $"Tag '{fullPath}' must be in the form 'LEVEL/SUBLEVEL/TAGNAME.DJ' (e.g. 'REGIAO_01/SE_01/BAY_03.STA.DJ').");

        var level = "/" + path[..lastSlash].Trim('/');
        var tagName = path[(lastSlash + 1)..];

        if (!tagName.EndsWith(BreakerSuffix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Tag '{fullPath}' is not a circuit breaker: breaker tag names end with '{BreakerSuffix}'.");

        return new BreakerTag(level, tagName);
    }

    public override string ToString() => FullPath;
}
