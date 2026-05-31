namespace Hunting.Data;

using System.Text.RegularExpressions;
using Hunting.Core.Render;

internal static partial class RenderDirectiveParser
{
    private static readonly HashSet<string> SupportedKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "table", "card", "timechart", "linechart", "barchart", "columnchart", "piechart", "areachart", "scatterchart"
    };

    public static (string QueryWithoutRender, RenderSpec Spec) Extract(string kql)
    {
        var match = RenderRegex().Match(kql);
        if (!match.Success)
        {
            return ContainsRenderToken(kql)
                ? (kql, RenderSpecDefaults.Table("Render clause must be terminal and use key=value properties."))
                : (kql, RenderSpecDefaults.Table());
        }

        var kindRaw = match.Groups["kind"].Value;
        if (!SupportedKinds.Contains(kindRaw))
        {
            var strippedUnsupported = kql[..match.Index].TrimEnd();
            return (strippedUnsupported, RenderSpecDefaults.Table($"Unsupported render kind '{kindRaw}'."));
        }

        var (properties, malformedProperty) = ParseProperties(match.Groups["props"].Value, match.Groups["withProps"].Value);
        if (malformedProperty is not null)
        {
            var strippedMalformed = kql[..match.Index].TrimEnd();
            return (strippedMalformed, RenderSpecDefaults.Table($"Malformed render property '{malformedProperty}'. Expected key=value."));
        }

        var stripped = kql[..match.Index].TrimEnd();

        var spec = new RenderSpec(
            ParseKind(kindRaw),
            Get(properties, "title"),
            Get(properties, "xcolumn"),
            SplitCsv(Get(properties, "ycolumns")),
            Get(properties, "series"),
            Get(properties, "legend"),
            string.Equals(Get(properties, "kind"), "stacked", StringComparison.OrdinalIgnoreCase),
            false,
            null);

        return (stripped, spec);
    }

    private static RenderKind ParseKind(string raw) => raw.ToLowerInvariant() switch
    {
        "table" => RenderKind.Table,
        "card" => RenderKind.Card,
        "timechart" => RenderKind.Timechart,
        "linechart" => RenderKind.Linechart,
        "barchart" => RenderKind.Barchart,
        "columnchart" => RenderKind.Columnchart,
        "piechart" => RenderKind.Piechart,
        "areachart" => RenderKind.Areachart,
        "scatterchart" => RenderKind.Scatterchart,
        _ => RenderKind.Table
    };

    private static (Dictionary<string, string> Properties, string? MalformedProperty) ParseProperties(string legacyInput, string withInput)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(legacyInput) && string.IsNullOrWhiteSpace(withInput))
        {
            return (map, null);
        }

        foreach (Match propertyMatch in LegacyPropertyRegex().Matches(legacyInput))
        {
            var key = propertyMatch.Groups["key"].Value;
            var rawValue = propertyMatch.Groups["value"].Value;
            map[key] = rawValue.Trim('"', '\'');
        }

        var legacyRemainder = LegacyPropertyRegex().Replace(legacyInput, string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(legacyRemainder))
        {
            return (map, legacyRemainder);
        }

        foreach (Match propertyMatch in WithPropertyRegex().Matches(withInput))
        {
            var key = propertyMatch.Groups["key"].Value;
            var rawValue = propertyMatch.Groups["value"].Value;
            map[key] = rawValue.Trim('"', '\'');
        }

        var withRemainder = WithPropertyRegex().Replace(withInput, string.Empty).Trim().Trim(',');
        return string.IsNullOrWhiteSpace(withRemainder) ? (map, null) : (map, withRemainder);
    }

    private static bool ContainsRenderToken(string kql)
        => Regex.IsMatch(kql, @"\|\s*render\b", RegexOptions.IgnoreCase);

    private static string? Get(IReadOnlyDictionary<string, string> props, string key)
        => props.TryGetValue(key, out var value) ? value : null;

    private static IReadOnlyList<string> SplitCsv(string? csv)
        => string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [GeneratedRegex(@"\|\s*render\s+(?<kind>[A-Za-z][A-Za-z0-9_-]*)(?:(?<props>(?:\s+[A-Za-z][A-Za-z0-9_-]*\s*=\s*(?:'(?:\\.|[^'\\])*'|""(?:\\.|[^""\\])*""|[^\s|;]+))*))?(?:\s+with\s*\((?<withProps>[^)]*)\))?\s*;?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex RenderRegex();

    [GeneratedRegex(@"\s+(?<key>[A-Za-z][A-Za-z0-9_-]*)\s*=\s*(?<value>'(?:\\.|[^'\\])*'|""(?:\\.|[^""\\])*""|[^\s|;]+)")]
    private static partial Regex LegacyPropertyRegex();

    [GeneratedRegex(@"(?:^|,)\s*(?<key>[A-Za-z][A-Za-z0-9_-]*)\s*=\s*(?<value>'(?:\\.|[^'\\])*'|""(?:\\.|[^""\\])*""|[^,\s][^,]*)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex WithPropertyRegex();
}