
using System.Text.RegularExpressions;
using DeltaZulu.Platform.Application.Analytics.Rendering.Directives;
using DeltaZulu.Platform.Domain.Analytics.Rendering;

namespace DeltaZulu.Platform.Application.Analytics.Render.Directives;
public sealed partial class RenderDirectiveParser : IRenderDirectiveParser
{
    private static readonly HashSet<string> SupportedKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "table", "card", "timechart", "linechart", "barchart", "columnchart", "piechart", "areachart", "scatterchart"
    };

    public RenderDirectiveParseResult Parse(string queryText)
    {
        ArgumentNullException.ThrowIfNull(queryText);

        var match = RenderRegex().Match(queryText);
        if (!match.Success)
        {
            return new RenderDirectiveParseResult
            {
                QueryTextWithoutRender = queryText,
                HasRenderDirective = ContainsRenderToken(queryText),
                Directive = ContainsRenderToken(queryText)
                    ? RenderDirective.Table("Render clause must be terminal and use key=value properties.")
                    : RenderDirective.Table()
            };
        }

        var tail = match.Groups["tail"].Value;
        if (ContainsPipeToken(tail))
        {
            return new RenderDirectiveParseResult
            {
                QueryTextWithoutRender = queryText,
                HasRenderDirective = true,
                Directive = RenderDirective.Table("Render clause must be terminal and use key=value properties.")
            };
        }

        var kindRaw = match.Groups["kind"].Value;
        if (string.IsNullOrWhiteSpace(kindRaw))
        {
            kindRaw = "table";
        }

        if (!SupportedKinds.Contains(kindRaw))
        {
            return new RenderDirectiveParseResult
            {
                QueryTextWithoutRender = queryText[..match.Index].TrimEnd(),
                HasRenderDirective = true,
                Directive = RenderDirective.Table($"Unsupported render kind '{kindRaw}'.")
            };
        }

        var (legacyProperties, withProperties) = SplitPropertySections(tail);
        var (properties, malformedProperty) = ParseProperties(legacyProperties, withProperties);
        if (malformedProperty is not null)
        {
            return new RenderDirectiveParseResult
            {
                QueryTextWithoutRender = queryText[..match.Index].TrimEnd(),
                HasRenderDirective = true,
                Directive = RenderDirective.Table($"Malformed render property '{malformedProperty}'. Expected key=value.")
            };
        }

        return new RenderDirectiveParseResult
        {
            QueryTextWithoutRender = queryText[..match.Index].TrimEnd(),
            HasRenderDirective = true,
            Directive = new RenderDirective
            {
                Kind = ParseKind(kindRaw),
                Title = Get(properties, "title"),
                Binding = new RenderBinding
                {
                    XColumn = Get(properties, "xcolumn"),
                    YColumns = SplitCsv(Get(properties, "ycolumns")),
                    SeriesColumn = Get(properties, "series")
                },
                Legend = Get(properties, "legend"),
                IsStacked = string.Equals(Get(properties, "kind"), "stacked", StringComparison.OrdinalIgnoreCase)
            }
        };
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

    private static (string LegacyProperties, string WithProperties) SplitPropertySections(string tail)
    {
        if (string.IsNullOrWhiteSpace(tail))
        {
            return (string.Empty, string.Empty);
        }

        var match = WithClauseRegex().Match(tail);
        return match.Success
            ? (match.Groups["legacy"].Value, match.Groups["withProps"].Value)
            : (tail, string.Empty);
    }

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

    private static bool ContainsRenderToken(string queryText)
        => Regex.IsMatch(queryText, @"\|\s*render\b", RegexOptions.IgnoreCase);

    private static bool ContainsPipeToken(string tail)
        => tail.Contains('|', StringComparison.Ordinal);

    private static string? Get(IReadOnlyDictionary<string, string> props, string key)
        => props.TryGetValue(key, out var value) ? value : null;

    private static IReadOnlyList<string> SplitCsv(string? csv)
        => string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [GeneratedRegex(@"\|\s*render(?:\s+(?<kind>[A-Za-z][A-Za-z0-9_-]*))?(?<tail>.*?)\s*;?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex RenderRegex();

    [GeneratedRegex(@"^(?<legacy>.*?)(?:\s+with\s*\((?<withProps>.*)\))?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex WithClauseRegex();

    [GeneratedRegex(@"\s+(?<key>[A-Za-z][A-Za-z0-9_-]*)\s*=\s*(?<value>'(?:\\.|[^'\\])*'|""(?:\\.|[^""\\])*""|[^\s|;]+)")]
    private static partial Regex LegacyPropertyRegex();

    [GeneratedRegex(@"(?:^|,)\s*(?<key>[A-Za-z][A-Za-z0-9_-]*)\s*=\s*(?<value>'(?:\\.|[^'\\])*'|""(?:\\.|[^""\\])*""|.*?)(?=\s*,\s*[A-Za-z][A-Za-z0-9_-]*\s*=|\s*$)", RegexOptions.IgnoreCase)]
    private static partial Regex WithPropertyRegex();
}