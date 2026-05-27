namespace Hunting.Data;

using System.Text.RegularExpressions;
using Hunting.Core.Render;

internal static partial class RenderDirectiveParser
{
    private static readonly HashSet<string> SupportedKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "table", "card", "timechart", "linechart", "barchart", "columnchart", "piechart"
    };

    public static (string QueryWithoutRender, RenderSpec Spec) Extract(string kql)
    {
        var match = RenderRegex().Match(kql);
        if (!match.Success)
        {
            return (kql, RenderSpecDefaults.Table());
        }

        var kindRaw = match.Groups["kind"].Value;
        if (!SupportedKinds.Contains(kindRaw))
        {
            var strippedUnsupported = kql[..match.Index].TrimEnd();
            return (strippedUnsupported, RenderSpecDefaults.Table($"Unsupported render kind '{kindRaw}'."));
        }

        var properties = ParseProperties(match.Groups["props"].Value);
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
        _ => RenderKind.Table
    };

    private static Dictionary<string, string> ParseProperties(string input)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(input)) return map;
        var items = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var item in items)
        {
            var split = item.Split('=', 2, StringSplitOptions.TrimEntries);
            if (split.Length == 2)
            {
                map[split[0]] = split[1].Trim('\'', '"');
            }
        }

        return map;
    }

    private static string? Get(IReadOnlyDictionary<string, string> props, string key)
        => props.TryGetValue(key, out var value) ? value : null;

    private static IReadOnlyList<string> SplitCsv(string? csv)
        => string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [GeneratedRegex(@"\|\s*render\s+(?<kind>[A-Za-z][A-Za-z0-9_-]*)(?<props>(?:\s+[A-Za-z][A-Za-z0-9_-]*\s*=\s*(?:'(?:\\.|[^'\\])*'|""(?:\\.|[^""\\])*""|[^\s|;]+))*)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex RenderRegex();
}
